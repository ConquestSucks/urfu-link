<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=false; section>
    <#if section = "header">
        ${kcSanitize(msg("errorTitle"))?no_esc}
    <#elseif section = "form">
        <div class="alert alert-error">
            <#if message?? && message.summary??>
                ${kcSanitize(message.summary)?no_esc}
            <#else>
                ${msg("errorTitle")}
            </#if>
        </div>
        <#if skipLink??>
        <#else>
            <#if client?? && client.baseUrl?has_content>
                <a href="${client.baseUrl}" class="btn-primary" style="display: block; text-align: center; text-decoration: none; line-height: 44px;">
                    ${msg("backToApplication")}
                </a>
            </#if>
        </#if>
    </#if>
</@layout.registrationLayout>
