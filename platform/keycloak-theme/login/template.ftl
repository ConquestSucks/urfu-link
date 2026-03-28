<#macro registrationLayout bodyClass="" displayInfo=false displayMessage=true displayRequiredFields=false>
<!DOCTYPE html>
<html lang="${(locale.currentLanguageTag)!'ru'}">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="robots" content="noindex, nofollow">
    <title>${msg("loginTitle", (realm.displayName!''))}</title>
    <link rel="icon" href="${url.resourcesPath}/img/logo.svg" type="image/svg+xml">
    <link rel="stylesheet" href="${url.resourcesPath}/css/login.css">
</head>
<body>
    <div class="login-bg"></div>

    <div class="login-page">
        <div class="login-logo">
            <img src="${url.resourcesPath}/img/logo.svg" alt="${realm.displayName!''}" height="40">
        </div>

        <div class="login-card">
            <div class="login-card-header">
                <h1 class="login-card-title"><#nested "header"></h1>
                <#if client?? && client.name?has_content>
                    <div class="client-info">
                        <span>&rarr;</span>
                        <#if client.baseUrl?has_content>
                            <a href="${client.baseUrl}">${client.name}</a>
                        <#else>
                            <span>${client.name}</span>
                        </#if>
                    </div>
                </#if>
            </div>

            <#if displayMessage && message?has_content && (message.type != 'warning' || !isAppInitiatedAction??)>
                <div class="alert alert-${message.type}">
                    ${kcSanitize(message.summary)?no_esc}
                </div>
            </#if>

            <#nested "form">
        </div>

        <#if displayInfo>
            <div class="login-info">
                <#nested "info">
            </div>
        </#if>

        <div class="login-footer">
            <span class="login-footer-text">urfu-link.ghjc.ru</span>
        </div>
    </div>
</body>
</html>
</#macro>
