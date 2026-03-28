<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('totp'); section>
    <#if section = "header">
        ${msg("doLogIn")}
    <#elseif section = "form">
        <form id="kc-otp-login-form" action="${url.loginAction}" method="post">
            <#if otpLogin.userOtpCredentials?size gt 1>
                <div class="otp-credentials">
                    <#list otpLogin.userOtpCredentials as otpCredential>
                        <label class="otp-credential <#if otpCredential.id == otpLogin.selectedCredentialId>selected</#if>">
                            <input id="kc-otp-credential-${otpCredential?index}"
                                   type="radio" name="selectedCredentialId"
                                   value="${otpCredential.id}"
                                   <#if otpCredential.id == otpLogin.selectedCredentialId>checked</#if>>
                            <span class="otp-credential-label">${otpCredential.userLabel}</span>
                        </label>
                    </#list>
                </div>
            </#if>

            <div class="form-group">
                <label for="otp" class="form-label">${msg("loginOtpOneTime")}</label>
                <div class="form-input-wrapper">
                    <input id="otp" name="otp" type="text"
                           class="form-input"
                           autocomplete="one-time-code" inputmode="numeric"
                           autofocus
                           aria-invalid="<#if messagesPerField.existsError('totp')>true</#if>"
                           dir="ltr" />
                </div>
                <#if messagesPerField.existsError('totp')>
                    <span class="input-error" aria-live="polite">
                        ${kcSanitize(messagesPerField.get('totp'))?no_esc}
                    </span>
                </#if>
            </div>

            <button name="login" id="kc-login" type="submit" class="btn-primary">
                ${msg("doLogIn")}
            </button>
        </form>
    </#if>
</@layout.registrationLayout>
