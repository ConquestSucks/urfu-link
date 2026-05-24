namespace ContractTests;

public sealed class DeploymentContractTests
{
    [Fact]
    public void HelmValuesShouldAlignWithPromotedImageTags()
    {
        var prodValues = ReadRepoFile("deploy", "helm", "services", "user-service", "values-prod.yaml");
        var devValues = ReadRepoFile("deploy", "helm", "services", "user-service", "values-dev.yaml");

        Assert.Matches(@"tag:\s+sha-[0-9a-f]{7}", prodValues);
        Assert.Contains("tag: dev-local", devValues, StringComparison.Ordinal);
        Assert.Contains("secrets:", prodValues, StringComparison.Ordinal);
        Assert.Contains("secrets:", devValues, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalKubernetesOverlayShouldDefineClusterDependencies()
    {
        var overlay = ReadRepoFile("platform", "dev", "local-k8s", "kustomization.yaml");
        var dependencies = ReadRepoFile("platform", "dev", "local-k8s", "dependencies.yaml");

        Assert.Contains("dependencies.yaml", overlay, StringComparison.Ordinal);
        Assert.Contains("kind: Deployment", dependencies, StringComparison.Ordinal);
        Assert.Contains("name: kafka", dependencies, StringComparison.Ordinal);
        Assert.Contains("name: keycloak", dependencies, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionPomeriumRoutesShouldProxyApiAndSignalRHubsToGateway()
    {
        var config = ReadRepoFile("deploy", "k8s", "platform", "identity", "pomerium-config.yaml");

        Assert.Contains("prefix: /api", config, StringComparison.Ordinal);
        Assert.Contains("prefix: /hubs", config, StringComparison.Ordinal);
        Assert.Contains("allow_websockets: true", config, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionApiHostShouldResolveToGateway()
    {
        var frontendValues = ReadRepoFile("deploy", "helm", "services", "frontend-web", "values-prod.yaml");
        var easConfig = ReadRepoFile("apps", "client", "eas.json");
        var platformKustomization = ReadRepoFile("deploy", "k8s", "platform", "kustomization.yaml");
        var gatewayValues = ReadRepoFile("deploy", "helm", "services", "api-gateway", "values-prod.yaml");

        Assert.Contains("EXPO_PUBLIC_API_URL: https://api.urfu-link.ghjc.ru", frontendValues, StringComparison.Ordinal);
        Assert.Contains("\"EXPO_PUBLIC_API_URL\": \"https://api.urfu-link.ghjc.ru\"", easConfig, StringComparison.Ordinal);
        Assert.Contains("ingress/api-gateway-ingress.yaml", platformKustomization, StringComparison.Ordinal);
        Assert.Contains("- ingress-nginx", gatewayValues, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationHookShouldNotDependOnWorkloadServiceAccount()
    {
        var migrationJob = ReadRepoFile("deploy", "helm", "charts", "urfu-service", "templates", "migrations-job.yaml");
        var migrationServiceAccount = ReadRepoFile(
            "deploy",
            "helm",
            "charts",
            "urfu-service",
            "templates",
            "migrations-serviceaccount.yaml");
        var chartValues = ReadRepoFile("deploy", "helm", "charts", "urfu-service", "values.yaml");

        Assert.Contains("serviceAccountName: {{ include \"urfu-service.migrationsServiceAccountName\" . }}", migrationJob, StringComparison.Ordinal);
        Assert.Contains("automountServiceAccountToken: {{ .Values.migrations.serviceAccount.automountServiceAccountToken }}", migrationJob, StringComparison.Ordinal);
        Assert.Contains("kind: ServiceAccount", migrationServiceAccount, StringComparison.Ordinal);
        Assert.Contains("helm.sh/hook", migrationServiceAccount, StringComparison.Ordinal);
        Assert.Contains("pre-install,pre-upgrade", migrationServiceAccount, StringComparison.Ordinal);
        Assert.Contains("\"helm.sh/hook-weight\": \"-10\"", migrationServiceAccount, StringComparison.Ordinal);
        Assert.Contains("automountServiceAccountToken: {{ .Values.migrations.serviceAccount.automountServiceAccountToken }}", migrationServiceAccount, StringComparison.Ordinal);
        Assert.Contains("automountServiceAccountToken: false", chartValues, StringComparison.Ordinal);
    }

    [Fact]
    public void KeycloakBootstrapPolicyShouldAllowWritingChatServiceInternalSecret()
    {
        var config = ReadRepoFile("deploy", "k8s", "platform", "vault", "vault-auto-init-job.yaml");
        var policyStart = config.IndexOf("vault policy write keycloak-bootstrap", StringComparison.Ordinal);
        policyStart = config.IndexOf("<<EOF_POL", policyStart, StringComparison.Ordinal) + "<<EOF_POL".Length;
        var policyEnd = config.IndexOf("EOF_POL", policyStart, StringComparison.Ordinal);
        var keycloakBootstrapPolicy = config[policyStart..policyEnd];

        Assert.Contains("path \"kv/data/urfu-link/prod/chat-service\"", keycloakBootstrapPolicy, StringComparison.Ordinal);
        Assert.Contains("capabilities = [\"create\", \"update\", \"read\"]", keycloakBootstrapPolicy, StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformStatefulShouldNotReconcileGeneratedMongoPassword()
    {
        var application = ReadRepoFile("deploy", "k8s", "platform", "argocd", "apps", "wave-5", "platform-stateful.yaml");

        Assert.Contains("name: mongo-app-user-password", application, StringComparison.Ordinal);
        Assert.Contains("/data/password", application, StringComparison.Ordinal);
        Assert.Contains("/stringData", application, StringComparison.Ordinal);
        Assert.Contains("RespectIgnoreDifferences=true", application, StringComparison.Ordinal);
    }

    [Fact]
    public void MongoStatefulSetShouldStartAfterPlatformBootstrapGeneratesPassword()
    {
        var manifest = ReadRepoFile("deploy", "k8s", "platform", "stateful", "mongodb-cluster.yaml");

        Assert.Contains("name: mongo-app-user-password", manifest, StringComparison.Ordinal);
        Assert.Contains("argocd.argoproj.io/sync-wave: \"-1\"", manifest, StringComparison.Ordinal);
        Assert.Contains("name: urfu-mongo", manifest, StringComparison.Ordinal);
        Assert.Contains("argocd.argoproj.io/sync-wave: \"3\"", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformBootstrapShouldProvisionPresencePostgresConnection()
    {
        var manifest = ReadRepoFile("deploy", "k8s", "platform", "stateful", "platform-bootstrap-job.yaml");

        Assert.Contains("presence_db_password", manifest, StringComparison.Ordinal);
        Assert.Contains("CREATE ROLE presence LOGIN PASSWORD '$PRESENCE_DB_PASSWORD'", manifest, StringComparison.Ordinal);
        Assert.Contains("CREATE DATABASE presence_db OWNER presence", manifest, StringComparison.Ordinal);
        Assert.Contains(
            "primary_connection\":\"Host=urfu-postgres-rw.urfu-platform.svc.cluster.local;Port=5432;Database=presence_db;Username=presence;Password=%s",
            manifest,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PRESENCE_PAYLOAD='{\"data\":{\"primary_connection\":\"urfu-redis.urfu-platform.svc.cluster.local:6379\"}}'",
            manifest,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformBootstrapShouldProvisionNotificationPostgresConnection()
    {
        var bootstrap = ReadRepoFile("deploy", "k8s", "platform", "stateful", "platform-bootstrap-job.yaml");
        var externalSecrets = ReadRepoFile("deploy", "k8s", "platform", "external-secrets", "services-external-secrets.yaml");

        Assert.Contains("notification_db_password", bootstrap, StringComparison.Ordinal);
        Assert.Contains("CREATE ROLE notification LOGIN PASSWORD '$NOTIFICATION_DB_PASSWORD'", bootstrap, StringComparison.Ordinal);
        Assert.Contains("CREATE DATABASE notification_db OWNER notification", bootstrap, StringComparison.Ordinal);
        Assert.Contains(
            "primary_connection\":\"Host=urfu-postgres-rw.urfu-platform.svc.cluster.local;Port=5432;Database=notification_db;Username=notification;Password=%s",
            bootstrap,
            StringComparison.Ordinal);

        var notificationSecretStart = externalSecrets.IndexOf("name: notification-service-secrets", StringComparison.Ordinal);
        var notificationSecretEnd = externalSecrets.IndexOf("---", notificationSecretStart, StringComparison.Ordinal);
        var notificationSecret = externalSecrets[notificationSecretStart..notificationSecretEnd];

        Assert.Contains("secretKey: ConnectionStrings__Primary", notificationSecret, StringComparison.Ordinal);
        Assert.Contains("key: urfu-link/prod/notification-service", notificationSecret, StringComparison.Ordinal);
        Assert.Contains("property: primary_connection", notificationSecret, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] pathSegments)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

        return File.ReadAllText(Path.Combine([repoRoot, .. pathSegments]));
    }
}
