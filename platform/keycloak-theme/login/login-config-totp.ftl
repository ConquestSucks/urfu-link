<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('totp','userLabel'); section>
    <#if section = "header">
        ${msg("loginTotpTitle")}
    <#elseif section = "form">
        <div class="totp-setup">
            <ol class="instruction" style="text-align: left; padding-left: 20px; margin-bottom: 20px;">
                <li>${msg("loginTotpStep1")}</li>
                <li>${msg("loginTotpStep2")}</li>
                <li>${msg("loginTotpStep3")}</li>
            </ol>

            <#if mode?? && mode = "manual">
                <div class="totp-secret">${totp.totpSecretEncoded}</div>
                <div class="totp-params">
                    ${msg("loginTotpType")}: ${msg("loginTotp." + totp.policy.type)}
                    <br>${msg("loginTotpAlgorithm")}: ${totp.policy.getAlgorithmKey()}
                    <br>${msg("loginTotpDigits")}: ${totp.policy.digits}
                    <#if totp.policy.type = "totp">
                        <br>${msg("loginTotpInterval")}: ${totp.policy.period}
                    <#elseif totp.policy.type = "hotp">
                        <br>${msg("loginTotpCounter")}: ${totp.policy.initialCounter}
                    </#if>
                </div>
                <a href="${totp.qrUrl}" class="form-link">${msg("loginTotpScanBarcode")}</a>
            <#else>
                <div class="totp-qr">
                    <img src="data:image/png;base64, ${totp.totpSecretQrCode}" alt="QR Code" width="200" height="200">
                </div>
                <a href="${totp.manualUrl}" class="form-link">${msg("loginTotpUnableToScan")}</a>
            </#if>
        </div>

        <form id="kc-totp-settings-form" action="${url.loginAction}" method="post" style="margin-top: 24px;">
            <div class="form-group">
                <label for="totp" class="form-label">${msg("authenticatorCode")}</label>
                <div class="form-input-wrapper">
                    <input id="totp" name="totp" type="text"
                           class="form-input"
                           autocomplete="off" inputmode="numeric"
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

            <div class="form-group">
                <label for="userLabel" class="form-label">${msg("loginTotpDeviceName")}</label>
                <div class="form-input-wrapper">
                    <input id="userLabel" name="userLabel" type="text"
                           class="form-input"
                           autocomplete="off"
                           aria-invalid="<#if messagesPerField.existsError('userLabel')>true</#if>" />
                </div>
                <#if messagesPerField.existsError('userLabel')>
                    <span class="input-error" aria-live="polite">
                        ${kcSanitize(messagesPerField.get('userLabel'))?no_esc}
                    </span>
                </#if>
            </div>

            <input type="hidden" name="totpSecret" value="${totp.totpSecret}">
            <#if mode??><input type="hidden" name="mode" value="${mode}"></#if>

            <#if isAppInitiatedAction??>
                <div class="btn-group">
                    <button type="submit" class="btn-primary">${msg("doSubmit")}</button>
                    <button type="submit" name="cancel-aia" value="true" class="btn-secondary">${msg("doCancel")}</button>
                </div>
            <#else>
                <button type="submit" class="btn-primary">${msg("doSubmit")}</button>
            </#if>
        </form>
    </#if>
</@layout.registrationLayout>
