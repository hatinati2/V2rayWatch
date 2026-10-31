package com.linkwatch.app

import android.content.Context

/** User-selectable interface language. Persian remains the first-launch default. */
enum class AppLanguage(val tag: String) {
    PERSIAN("fa"),
    ENGLISH("en");

    val isEnglish: Boolean get() = this == ENGLISH

    fun toggle(): AppLanguage = if (this == PERSIAN) ENGLISH else PERSIAN

    companion object {
        private const val PREFS = "v2raywatch_ui"
        private const val KEY_LANGUAGE = "language_tag"

        fun load(context: Context): AppLanguage {
            val tag = context.applicationContext
                .getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                .getString(KEY_LANGUAGE, PERSIAN.tag)
            return entries.firstOrNull { it.tag == tag } ?: PERSIAN
        }

        fun save(context: Context, language: AppLanguage) {
            context.applicationContext
                .getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                .edit()
                .putString(KEY_LANGUAGE, language.tag)
                .apply()
        }
    }
}

fun uiText(language: AppLanguage, persian: String, english: String): String =
    if (language.isEnglish) english else persian

fun uiError(language: AppLanguage, error: String): String {
    if (!language.isEnglish || error.isBlank()) return error
    val translations = listOf(
        "هیچ لینک پشتیبانی‌شده‌ای پیدا نشد" to "No supported links were found",
        "فرمت‌های فعلی: VLESS، VMess، Trojan، Shadowsocks و Hysteria2." to "Supported formats: VLESS, VMess, Trojan, Shadowsocks, and Hysteria2.",
        "لینک خالی است" to "The link is empty",
        "فرمت لینک پشتیبانی نمی‌شود" to "This link format is not supported",
        "مقداری وارد نشده است" to "No value was provided",
        "برای امنیت، آدرس Subscription باید HTTPS باشد" to "Subscription URLs must use HTTPS for security",
        "تعداد تغییرمسیرهای Subscription بیش از حد مجاز است" to "The subscription exceeded the redirect limit",
        "مسیر تغییرمقصد Subscription ناقص است" to "The subscription redirect is incomplete",
        "آدرس تغییرمقصد Subscription نامعتبر است" to "The subscription redirect URL is invalid",
        "تغییرمقصد Subscription به HTTP ناامن رد شد" to "An insecure HTTP subscription redirect was blocked",
        "دریافت Subscription ناموفق بود" to "Failed to fetch the subscription",
        "پاسخ Subscription خالی است" to "The subscription response is empty",
        "حجم Subscription بیش از ۲ مگابایت است" to "The subscription is larger than 2 MB",
        "UUID در لینک VLESS خالی است" to "The VLESS link has no UUID",
        "رمز Trojan خالی است" to "The Trojan password is empty",
        "اطلاعات VMess ناقص است" to "The VMess configuration is incomplete",
        "رمز احراز هویت Hysteria2 در لینک خالی است" to "The Hysteria2 authentication password is empty",
        "JSON فاقد outbounds است" to "The JSON has no outbounds section",
        "JSON هیچ outboundای ندارد" to "The JSON contains no outbound entries",
        "اولین outbound معتبر نیست" to "The first outbound entry is invalid",
        "آدرس سرور در لینک معتبر نیست" to "The server address in the link is invalid",
        "پورت سرور معتبر نیست" to "The server port is invalid",
        "تست اتصال واقعی ناموفق بود یا مهلت پاسخ تمام شد" to "The real connection test failed or timed out",
        "شناسه امن دستگاه برای مقداردهی Xray در دسترس نیست" to "A device identifier required by Xray is unavailable"
    )
    var result = error
    translations.forEach { (fa, en) -> result = result.replace(fa, en) }
    return if (result.any { it in '\u0600'..'\u06FF' }) "The operation failed. Check the configuration and try again." else result
}
