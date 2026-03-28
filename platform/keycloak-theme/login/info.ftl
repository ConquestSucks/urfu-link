<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=false; section>
    <#if section = "header">
        <#if messageHeader??>
            ${kcSanitize(msg("${messageHeader}"))?no_esc}
        <#else>
            ${(message.summary)!''}
        </#if>
    <#elseif section = "form">
        <div class="alert alert-info">
            ${(message.summary)!''}
            <#if requiredActions??>
                <#list requiredActions>
                    : <strong><#items as reqActionItem>${kcSanitize(msg("requiredAction.${reqActionItem}"))?no_esc}<#sep>, </#items></strong>
                </#list>
            </#if>
        </div>
        <#if skipLink??>
        <#else>
            <#if pageRedirectUri?has_content>
                <a href="${pageRedirectUri}" class="btn-primary" style="display: block; text-align: center; text-decoration: none; line-height: 44px;">
                    ${msg("backToApplication")}
                </a>
            <#elseif actionUri?has_content>
                <a href="${actionUri}" class="btn-primary" style="display: block; text-align: center; text-decoration: none; line-height: 44px;">
                    ${msg("proceedWithAction")}
                </a>
            <#elseif (client.baseUrl)?has_content>
                <a href="${client.baseUrl}" class="btn-primary" style="display: block; text-align: center; text-decoration: none; line-height: 44px;">
                    ${msg("backToApplication")}
                </a>
            </#if>
        </#if>
    </#if>
</@layout.registrationLayout>
