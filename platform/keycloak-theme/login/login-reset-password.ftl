<#import "template.ftl" as layout>
<@layout.registrationLayout displayInfo=true displayMessage=!messagesPerField.existsError('username'); section>
    <#if section = "header">
        ${msg("emailForgotTitle")}
    <#elseif section = "form">
        <form id="kc-reset-password-form" action="${url.loginAction}" method="post">
            <div class="form-group">
                <label for="username" class="form-label">
                    <#if !realm.loginWithEmailAllowed>
                        ${msg("username")}
                    <#elseif !realm.registrationEmailAsUsername>
                        ${msg("usernameOrEmail")}
                    <#else>
                        ${msg("email")}
                    </#if>
                </label>
                <div class="form-input-wrapper">
                    <input id="username" name="username" type="text"
                           class="form-input"
                           value="${(auth.attemptedUsername!'')}"
                           autofocus autocomplete="username"
                           aria-invalid="<#if messagesPerField.existsError('username')>true</#if>"
                           dir="ltr" />
                </div>
                <#if messagesPerField.existsError('username')>
                    <span class="input-error" aria-live="polite">
                        ${kcSanitize(messagesPerField.get('username'))?no_esc}
                    </span>
                </#if>
            </div>

            <div style="display: flex; flex-direction: column; gap: 12px;">
                <button type="submit" class="btn-primary">${msg("doSubmit")}</button>
                <a href="${url.loginUrl}" class="form-link" style="text-align: center;">${msg("backToLogin")}</a>
            </div>
        </form>
    <#elseif section = "info">
        <#if realm.duplicateEmailsAllowed>
            ${msg("emailInstructionUsername")}
        <#else>
            ${msg("emailInstruction")}
        </#if>
    </#if>
</@layout.registrationLayout>
