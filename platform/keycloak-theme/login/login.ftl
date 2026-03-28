<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('username','password'); section>
    <#if section = "header">
        ${msg("loginAccountTitle")}
    <#elseif section = "form">
        <form id="kc-form-login" action="${url.loginAction}" method="post">
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
                           value="${(login.username!'')}"
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

            <div class="form-group">
                <label for="password" class="form-label">${msg("password")}</label>
                <div class="form-input-wrapper form-input-password">
                    <input id="password" name="password" type="password"
                           class="form-input"
                           autocomplete="current-password"
                           aria-invalid="<#if messagesPerField.existsError('password')>true</#if>"
                           dir="ltr" />
                </div>
                <#if messagesPerField.existsError('password')>
                    <span class="input-error" aria-live="polite">
                        ${kcSanitize(messagesPerField.get('password'))?no_esc}
                    </span>
                </#if>
            </div>

            <div class="form-actions">
                <#if realm.rememberMe && !usernameHidden??>
                    <div class="form-checkbox-wrapper">
                        <input id="rememberMe" name="rememberMe" type="checkbox"
                               <#if login.rememberMe??>checked</#if>>
                        <label for="rememberMe">${msg("rememberMe")}</label>
                    </div>
                </#if>

                <#if realm.resetPasswordAllowed>
                    <a href="${url.loginResetCredentialsUrl}" class="form-link">${msg("doForgotPassword")}</a>
                </#if>
            </div>

            <input name="credentialId" type="hidden" value="${(auth.selectedCredential!'')}">
            <button id="kc-login" name="login" type="submit" class="btn-primary">
                ${msg("doLogIn")}
            </button>
        </form>
    </#if>
</@layout.registrationLayout>
