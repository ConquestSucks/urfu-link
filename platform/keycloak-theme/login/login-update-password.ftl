<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('password','password-confirm'); section>
    <#if section = "header">
        ${msg("updatePasswordTitle")}
    <#elseif section = "form">
        <form id="kc-passwd-update-form" action="${url.loginAction}" method="post">
            <div class="form-group">
                <label for="password-new" class="form-label">${msg("passwordNew")}</label>
                <div class="form-input-wrapper form-input-password">
                    <input id="password-new" name="password-new" type="password"
                           class="form-input"
                           autofocus autocomplete="new-password"
                           aria-invalid="<#if messagesPerField.existsError('password','password-confirm')>true</#if>"
                           dir="ltr" />
                </div>
                <#if messagesPerField.existsError('password')>
                    <span class="input-error" aria-live="polite">
                        ${kcSanitize(messagesPerField.get('password'))?no_esc}
                    </span>
                </#if>
            </div>

            <div class="form-group">
                <label for="password-confirm" class="form-label">${msg("passwordConfirm")}</label>
                <div class="form-input-wrapper form-input-password">
                    <input id="password-confirm" name="password-confirm" type="password"
                           class="form-input"
                           autocomplete="new-password"
                           aria-invalid="<#if messagesPerField.existsError('password-confirm')>true</#if>"
                           dir="ltr" />
                </div>
                <#if messagesPerField.existsError('password-confirm')>
                    <span class="input-error" aria-live="polite">
                        ${kcSanitize(messagesPerField.get('password-confirm'))?no_esc}
                    </span>
                </#if>
            </div>

            <#if isAppInitiatedAction??>
                <div class="btn-group">
                    <button name="login" type="submit" class="btn-primary">${msg("doSubmit")}</button>
                    <button type="submit" name="cancel-aia" value="true" class="btn-secondary">${msg("doCancel")}</button>
                </div>
            <#else>
                <button name="login" type="submit" class="btn-primary">${msg("doSubmit")}</button>
            </#if>
        </form>
    </#if>
</@layout.registrationLayout>
