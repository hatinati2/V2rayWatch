package com.linkwatch.app.core

import android.util.Base64
import com.linkwatch.app.data.ProxyProfile
import okhttp3.OkHttpClient
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.Request
import org.json.JSONArray
import org.json.JSONObject
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.ByteArrayOutputStream
import java.net.URI
import java.net.URLDecoder
import java.nio.charset.StandardCharsets
import java.util.concurrent.TimeUnit

object ProxyLinkParser {
    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(15, TimeUnit.SECONDS)
        .callTimeout(20, TimeUnit.SECONDS)
        .followRedirects(false)
        .followSslRedirects(false)
        .build()

    fun parseText(input: String): List<ProxyProfile> {
        val normalized = decodeSubscriptionIfNeeded(input.trim())
        if (normalized.startsWith("{")) return listOf(parseXrayJson(normalized))
        val lines = normalized.lineSequence()
            .map { it.trim() }
            .filter { it.isNotEmpty() && !it.startsWith("#") }
            .toList()

        val result = mutableListOf<ProxyProfile>()
        for (line in lines) {
            val parsed = runCatching { parseOne(line) }.getOrNull()
            if (parsed != null) result += parsed
        }
        if (result.isEmpty()) {
            throw IllegalArgumentException("هیچ لینک پشتیبانی‌شده‌ای پیدا نشد. فرمت‌های فعلی: VLESS، VMess، Trojan، Shadowsocks و Hysteria2.")
        }
        return result.distinctBy { it.rawLink }
    }

    fun parseOne(raw: String): ProxyProfile {
        val link = raw.trim()
        require(link.isNotEmpty()) { "لینک خالی است" }
        if (link.startsWith("{")) return parseXrayJson(link)

        return when {
            link.startsWith("vless://", true) -> parseVless(link)
            link.startsWith("vmess://", true) -> parseVmess(link)
            link.startsWith("trojan://", true) -> parseTrojan(link)
            link.startsWith("ss://", true) -> parseShadowsocks(link)
            link.startsWith("hysteria2://", true) || link.startsWith("hy2://", true) -> parseHysteria2(link)
            else -> throw IllegalArgumentException("فرمت لینک پشتیبانی نمی‌شود")
        }
    }

    suspend fun importInput(input: String): List<ProxyProfile> = withContext(Dispatchers.IO) {
        val value = input.trim()
        require(value.isNotEmpty()) { "مقداری وارد نشده است" }
        val content = when {
            value.startsWith("https://", true) -> fetchSubscription(value)
            value.startsWith("http://", true) -> throw IllegalArgumentException("برای امنیت، آدرس Subscription باید HTTPS باشد")
            else -> value
        }
        parseText(content)
    }

    private fun fetchSubscription(url: String): String {
        var current = url.toHttpUrl()
        require(current.isHttps) { "برای امنیت، آدرس Subscription باید HTTPS باشد" }
        val maxRedirects = 5
        for (redirectCount in 0..maxRedirects) {
            val request = Request.Builder()
                .url(current)
                .header("User-Agent", "V2rayWatch/0.2 Android")
                .get()
                .build()
            val content = httpClient.newCall(request).execute().use { response ->
                if (response.code in 300..399) {
                    if (redirectCount == maxRedirects) throw IllegalArgumentException("تعداد تغییرمسیرهای Subscription بیش از حد مجاز است")
                    val location = response.header("Location") ?: throw IllegalArgumentException("مسیر تغییرمقصد Subscription ناقص است")
                    val next = current.resolve(location) ?: throw IllegalArgumentException("آدرس تغییرمقصد Subscription نامعتبر است")
                    require(next.isHttps) { "تغییرمقصد Subscription به HTTP ناامن رد شد" }
                    current = next
                    null
                } else {
                    if (!response.isSuccessful) throw IllegalArgumentException("دریافت Subscription ناموفق بود: HTTP ${response.code}")
                    val body = response.body ?: throw IllegalArgumentException("پاسخ Subscription خالی است")
                    val maxBytes = 2 * 1024 * 1024
                    if (body.contentLength() > maxBytes) throw IllegalArgumentException("حجم Subscription بیش از ۲ مگابایت است")
                    val bytes = body.byteStream().use { input ->
                        val output = ByteArrayOutputStream()
                        val buffer = ByteArray(8 * 1024)
                        while (true) {
                            val count = input.read(buffer)
                            if (count < 0) break
                            if (output.size() + count > maxBytes) throw IllegalArgumentException("حجم Subscription بیش از ۲ مگابایت است")
                            output.write(buffer, 0, count)
                        }
                        output.toByteArray()
                    }
                    String(bytes, StandardCharsets.UTF_8)
                }
            }
            if (content != null) return content
        }
        throw IllegalArgumentException("دریافت Subscription ناموفق بود")
    }

    private fun decodeSubscriptionIfNeeded(value: String): String {
        if (value.contains("://")) return value
        val candidate = runCatching {
            val compact = value.filterNot { it.isWhitespace() }
            val decoded = Base64.decode(compact, Base64.DEFAULT or Base64.URL_SAFE)
            String(decoded, StandardCharsets.UTF_8)
        }.getOrNull()
        return if (candidate != null && candidate.contains("://")) candidate else value
    }

    private fun parseVless(link: String): ProxyProfile {
        val uri = URI(link)
        val host = requireHost(uri)
        val port = requirePort(uri)
        val uuid = decode(uri.rawUserInfo.orEmpty())
        require(uuid.isNotBlank()) { "UUID در لینک VLESS خالی است" }
        val q = query(uri.rawQuery).toMutableMap()
        val network = normalizeNetwork(q["type"] ?: q["network"] ?: "tcp")
        val security = (q["security"] ?: "none").lowercase()
        require(security in setOf("none", "tls", "reality")) { "روش امنیتی VLESS پشتیبانی نمی‌شود: $security" }
        if (q["sni"].isNullOrBlank()) q["sni"] = q["serverName"]?.takeIf { it.isNotBlank() } ?: host
        if (security == "reality") {
            require((q["pbk"] ?: q["publicKey"]).orEmpty().isNotBlank()) { "کلید عمومی Reality (pbk) در لینک وجود ندارد" }
            require(q["sni"].orEmpty().isNotBlank()) { "SNI برای Reality مشخص نشده است" }
        }
        val settings = JSONObject().put("vnext", JSONArray().put(
            JSONObject().put("address", host).put("port", port).put("users", JSONArray().put(
                JSONObject().put("id", uuid).put("encryption", q["encryption"] ?: "none").apply {
                    q["flow"]?.takeIf { it.isNotBlank() }?.let { put("flow", it) }
                    put("level", 0)
                }
            ))
        ))
        val outbound = JSONObject().put("tag", "proxy").put("protocol", "vless").put("settings", settings)
        val stream = makeStreamSettings(network, security, q)
        outbound.put("streamSettings", stream)
        return profile(link, uri.rawFragment, "VLESS", host, port, outbound)
    }

    private fun parseTrojan(link: String): ProxyProfile {
        val uri = URI(link)
        val host = requireHost(uri)
        val port = requirePort(uri)
        val password = decode(uri.rawUserInfo.orEmpty())
        require(password.isNotBlank()) { "رمز Trojan خالی است" }
        val q = query(uri.rawQuery).toMutableMap()
        val network = normalizeNetwork(q["type"] ?: q["network"] ?: "tcp")
        val security = (q["security"] ?: "tls").lowercase()
        require(security in setOf("none", "tls")) { "حالت امنیتی Trojan پشتیبانی نمی‌شود: $security" }
        if (q["sni"].isNullOrBlank()) q["sni"] = q["serverName"]?.takeIf { it.isNotBlank() } ?: host
        val server = JSONObject().put("address", host).put("port", port).put("password", password)
        val outbound = JSONObject()
            .put("tag", "proxy")
            .put("protocol", "trojan")
            .put("settings", JSONObject().put("servers", JSONArray().put(server)))
            .put("streamSettings", makeStreamSettings(network, security, q))
        return profile(link, uri.rawFragment, "Trojan", host, port, outbound)
    }

    private fun parseVmess(link: String): ProxyProfile {
        val encoded = link.substringAfter("vmess://", "").substringBefore('#').trim()
        val decoded = decodeBase64(encoded)
        val vmess = JSONObject(decoded)
        val host = vmess.optString("add").ifBlank { vmess.optString("address") }
        val port = vmess.optString("port").toIntOrNull() ?: vmess.optInt("port", -1)
        val id = vmess.optString("id")
        require(host.isNotBlank() && port in 1..65535 && id.isNotBlank()) { "اطلاعات VMess ناقص است" }

        val network = normalizeNetwork(vmess.optString("net", "tcp"))
        val tlsValue = vmess.optString("tls", "").lowercase()
        require(tlsValue.isBlank() || tlsValue == "tls") { "حالت TLS این VMess در نسخه فعلی پشتیبانی نمی‌شود" }
        val security = if (tlsValue == "tls") "tls" else "none"
        val settings = JSONObject().put("vnext", JSONArray().put(
            JSONObject().put("address", host).put("port", port).put("users", JSONArray().put(
                JSONObject()
                    .put("id", id)
                    .put("alterId", vmess.optInt("aid", 0))
                    .put("security", vmess.optString("scy", "auto").ifBlank { "auto" })
            ))
        ))
        val q = mutableMapOf<String, String>()
        q["sni"] = vmess.optString("sni").ifBlank { vmess.optString("host").ifBlank { host } }
        q["fp"] = vmess.optString("fp")
        q["alpn"] = vmess.optString("alpn")
        q["path"] = vmess.optString("path")
        q["host"] = vmess.optString("host")
        q["serviceName"] = vmess.optString("path")
        val outbound = JSONObject()
            .put("tag", "proxy")
            .put("protocol", "vmess")
            .put("settings", settings)
            .put("streamSettings", makeStreamSettings(network, security, q))
        return profile(link, vmess.optString("ps"), "VMess", host, port, outbound)
    }

    private fun parseShadowsocks(link: String): ProxyProfile {
        var candidate = link.substringAfter("ss://").substringBefore('#')
        if (!candidate.contains('@')) {
            candidate = decodeBase64(candidate)
        }
        val uri = URI("ss://$candidate")
        require(uri.rawQuery.isNullOrBlank()) { "افزونه‌های Shadowsocks در نسخه اول پشتیبانی نمی‌شوند" }
        val host = requireHost(uri)
        val port = requirePort(uri)
        val userInfo = decode(uri.rawUserInfo.orEmpty())
        val credentials = if (userInfo.contains(':')) userInfo else decodeBase64(userInfo)
        val method = credentials.substringBefore(':', "").trim()
        val password = credentials.substringAfter(':', "")
        require(method.isNotBlank() && password.isNotBlank()) { "روش رمزنگاری یا رمز Shadowsocks ناقص است" }
        val outbound = JSONObject()
            .put("tag", "proxy")
            .put("protocol", "shadowsocks")
            .put("settings", JSONObject().put("servers", JSONArray().put(
                JSONObject()
                    .put("address", host)
                    .put("port", port)
                    .put("method", method)
                    .put("password", password)
            )))
            .put("streamSettings", JSONObject().put("network", "tcp,udp"))
        return profile(link, link.substringAfter('#', "Shadowsocks"), "Shadowsocks", host, port, outbound)
    }


    /** Parses Hysteria2 share links using Xray's Hysteria v2 outbound. */
    private fun parseHysteria2(link: String): ProxyProfile {
        val normalized = if (link.startsWith("hy2://", true)) {
            "hysteria2://" + link.substringAfter("://")
        } else link
        val uri = URI(normalized)
        val host = requireHost(uri)
        val port = if (uri.port == -1) 443 else requirePort(uri)
        val auth = decode(uri.rawUserInfo.orEmpty())
        require(auth.isNotBlank()) { "رمز احراز هویت Hysteria2 در لینک خالی است" }

        val q = query(uri.rawQuery)
        val sni = (q["sni"] ?: q["peer"] ?: host).ifBlank { host }
        val alpn = q["alpn"].orEmpty().split(',').map { it.trim() }.filter { it.isNotEmpty() }
            .ifEmpty { listOf("h3") }
        val tls = JSONObject()
            .put("serverName", sni)
            .put("alpn", JSONArray(alpn))
        q["fp"]?.takeIf { it.isNotBlank() }?.let { tls.put("fingerprint", it) }
        if (q["insecure"] == "1" || q["insecure"].equals("true", true) || q["allowInsecure"] == "1") {
            tls.put("allowInsecure", true)
        }

        val stream = JSONObject()
            .put("network", "hysteria")
            .put("security", "tls")
            .put("tlsSettings", tls)
            .put("hysteriaSettings", JSONObject().put("version", 2).put("auth", auth))

        val finalMask = JSONObject()
        val obfsType = q["obfs"].orEmpty().lowercase()
        val obfsPassword = q["obfs-password"].orEmpty()
        if (obfsType == "salamander" && obfsPassword.isNotBlank()) {
            finalMask.put("udp", JSONArray().put(
                JSONObject().put("type", "salamander")
                    .put("settings", JSONObject().put("password", obfsPassword))
            ))
        }
        q["mport"]?.takeIf { it.isNotBlank() }?.let { ports ->
            val interval = q["mportInterval"]?.toIntOrNull()?.coerceAtLeast(5) ?: 30
            finalMask.put("quicParams", JSONObject().put(
                "udpHop", JSONObject().put("ports", ports).put("interval", interval.toString())
            ))
        }
        if (finalMask.length() > 0) stream.put("finalmask", finalMask)

        val outbound = JSONObject()
            .put("tag", "proxy")
            .put("protocol", "hysteria")
            .put("settings", JSONObject().put("version", 2).put("address", host).put("port", port))
            .put("streamSettings", stream)

        q["pinSHA256"]?.takeIf { it.isNotBlank() }?.let { tls.put("pinnedPeerCertSha256", it) }

        return profile(link, uri.rawFragment, "Hysteria2", host, port, outbound)
    }

    private fun parseXrayJson(link: String): ProxyProfile {
        val json = JSONObject(link)
        val outbounds = json.optJSONArray("outbounds") ?: throw IllegalArgumentException("JSON فاقد outbounds است")
        require(outbounds.length() > 0) { "JSON هیچ outboundای ندارد" }
        json.put("inbounds", JSONArray())
        val outbound = outbounds.optJSONObject(0) ?: throw IllegalArgumentException("اولین outbound معتبر نیست")
        val protocol = outbound.optString("protocol", "Custom")
        val hostPort = findAddress(outbound)
        val name = json.optString("remarks").ifBlank { "Custom $protocol" }
        val config = json.toString()
        return ProxyProfile(
            name = name,
            protocol = protocol.uppercase(),
            host = hostPort.first,
            port = hostPort.second,
            rawLink = link,
            configJson = config
        )
    }

    private fun findAddress(outbound: JSONObject): Pair<String, Int> {
        val settings = outbound.optJSONObject("settings") ?: return ("custom" to 0)
        val vnext = settings.optJSONArray("vnext")?.optJSONObject(0)
        if (vnext != null) return vnext.optString("address", "custom") to vnext.optInt("port", 0)
        val servers = settings.optJSONArray("servers")?.optJSONObject(0)
        if (servers != null) return servers.optString("address", "custom") to servers.optInt("port", 0)
        return "custom" to 0
    }

    private fun makeStreamSettings(network: String, security: String, q: Map<String, String>): JSONObject {
        val stream = JSONObject().put("network", network).put("security", if (security == "reality" || security == "tls") security else "none")
        if (security == "tls") {
            val tls = JSONObject()
            q["sni"]?.takeIf { it.isNotBlank() }?.let { tls.put("serverName", it) }
            q["fp"]?.takeIf { it.isNotBlank() }?.let { tls.put("fingerprint", it) }
            q["alpn"]?.split(',')?.map { it.trim() }?.filter { it.isNotEmpty() }?.takeIf { it.isNotEmpty() }?.let { tls.put("alpn", JSONArray(it)) }
            val allowInsecure = q["allowInsecure"] ?: q["insecure"]
            if (allowInsecure == "1" || allowInsecure.equals("true", true)) tls.put("allowInsecure", true)
            stream.put("tlsSettings", tls)
        } else if (security == "reality") {
            val reality = JSONObject()
            q["sni"]?.takeIf { it.isNotBlank() }?.let { reality.put("serverName", it) }
            q["fp"]?.takeIf { it.isNotBlank() }?.let { reality.put("fingerprint", it) }
            (q["pbk"] ?: q["publicKey"])?.takeIf { it.isNotBlank() }?.let { reality.put("publicKey", it) }
            (q["sid"] ?: q["shortId"])?.takeIf { it.isNotBlank() }?.let { reality.put("shortId", it) }
            (q["spx"] ?: q["spiderX"])?.takeIf { it.isNotBlank() }?.let { reality.put("spiderX", it) }
            stream.put("realitySettings", reality)
        }

        when (network) {
            "ws" -> {
                val ws = JSONObject()
                q["path"]?.takeIf { it.isNotBlank() }?.let { ws.put("path", it) }
                q["host"]?.takeIf { it.isNotBlank() }?.let { ws.put("headers", JSONObject().put("Host", it)) }
                stream.put("wsSettings", ws)
            }
            "grpc" -> {
                val grpc = JSONObject()
                (q["serviceName"] ?: q["path"])?.takeIf { it.isNotBlank() }?.let { grpc.put("serviceName", it) }
                q["mode"]?.takeIf { it.isNotBlank() }?.let { grpc.put("mode", it) }
                stream.put("grpcSettings", grpc)
            }
            "httpupgrade" -> {
                val upgrade = JSONObject()
                q["path"]?.takeIf { it.isNotBlank() }?.let { upgrade.put("path", it) }
                q["host"]?.takeIf { it.isNotBlank() }?.let { upgrade.put("host", it) }
                stream.put("httpupgradeSettings", upgrade)
            }
            "xhttp" -> {
                val xhttp = JSONObject()
                q["path"]?.takeIf { it.isNotBlank() }?.let { xhttp.put("path", it) }
                q["host"]?.takeIf { it.isNotBlank() }?.let { xhttp.put("host", it) }
                q["mode"]?.takeIf { it.isNotBlank() }?.let { xhttp.put("mode", it) }
                q["extra"]?.takeIf { it.isNotBlank() }?.let { extra ->
                    val parsedExtra = runCatching { JSONObject(extra) }.getOrNull()
                    if (parsedExtra != null) xhttp.put("extra", parsedExtra)
                }
                stream.put("xhttpSettings", xhttp)
            }
        }
        return stream
    }

    private fun profile(
        link: String,
        rawName: String?,
        protocol: String,
        host: String,
        port: Int,
        outbound: JSONObject
    ): ProxyProfile {
        val root = JSONObject()
            .put("log", JSONObject().put("loglevel", "error"))
            .put("inbounds", JSONArray())
            .put("outbounds", JSONArray().put(outbound).put(JSONObject().put("tag", "direct").put("protocol", "freedom")))
            .put("stats", JSONObject())
            .put("policy", JSONObject()
                .put("levels", JSONObject().put("0", JSONObject()
                    .put("handshake", 4)
                    .put("connIdle", 300)
                    .put("uplinkOnly", 1)
                    .put("downlinkOnly", 1)
                ))
                .put("system", JSONObject()
                    .put("statsOutboundUplink", true)
                    .put("statsOutboundDownlink", true)
                )
            )
            .put("routing", JSONObject().put("domainStrategy", "AsIs").put("rules", JSONArray()))
        val displayName = rawName?.let { decode(it) }?.trim()?.takeIf { it.isNotBlank() } ?: "$protocol · $host"
        return ProxyProfile(
            name = displayName.take(80),
            protocol = protocol,
            host = host,
            port = port,
            rawLink = link,
            configJson = root.toString()
        )
    }

    private fun normalizeNetwork(value: String): String {
        val normalized = when (value.lowercase()) {
            "websocket" -> "ws"
            "httpupgrade", "http-upgrade" -> "httpupgrade"
            "splithttp", "split-http" -> "xhttp"
            else -> value.lowercase()
        }
        require(normalized in setOf("tcp", "ws", "grpc", "kcp", "quic", "httpupgrade", "xhttp")) {
            "نوع انتقال $value در نسخه اول پشتیبانی نمی‌شود"
        }
        return normalized
    }

    private fun requireHost(uri: URI): String = uri.host?.takeIf { it.isNotBlank() }?.removePrefix("[")?.removeSuffix("]")
        ?: throw IllegalArgumentException("آدرس سرور در لینک معتبر نیست")

    private fun requirePort(uri: URI): Int = uri.port.takeIf { it in 1..65535 }
        ?: throw IllegalArgumentException("پورت سرور معتبر نیست")

    private fun query(raw: String?): Map<String, String> {
        if (raw.isNullOrBlank()) return emptyMap()
        return raw.split('&').mapNotNull { pair ->
            val key = pair.substringBefore('=')
            val value = pair.substringAfter('=', "")
            if (key.isBlank()) null else decode(key) to decode(value)
        }.toMap()
    }

    private fun decode(value: String): String = runCatching {
        URLDecoder.decode(value.replace("+", "%2B"), "UTF-8")
    }.getOrDefault(value)

    private fun decodeBase64(value: String): String {
        val cleaned = value.trim().replace('-', '+').replace('_', '/')
        val padded = cleaned + "=".repeat((4 - cleaned.length % 4) % 4)
        val bytes = Base64.decode(padded, Base64.DEFAULT)
        return String(bytes, StandardCharsets.UTF_8)
    }
}
