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
        var localUpScript = ReadRepoFile("scripts", "local-k8s-up.ps1");

        Assert.Contains("dependencies.yaml", overlay, StringComparison.Ordinal);
        Assert.Contains("kind: Deployment", dependencies, StringComparison.Ordinal);
        Assert.Contains("name: kafka", dependencies, StringComparison.Ordinal);
        Assert.Contains("name: keycloak", dependencies, StringComparison.Ordinal);
        Assert.Contains("deployment/minio", localUpScript, StringComparison.Ordinal);
        Assert.Contains("job/minio-bootstrap-buckets", localUpScript, StringComparison.Ordinal);
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
    public void ProductionMediaStorageShouldExposeBrowserReachablePresignedUrls()
    {
        var mediaValues = ReadRepoFile("deploy", "helm", "services", "media-service", "values-prod.yaml");
        var minioIngress = ReadRepoFile("deploy", "k8s", "platform", "ingress", "minio-ingress.yaml");
        var minioTenant = ReadRepoFile("deploy", "k8s", "platform", "stateful", "minio-tenant.yaml");
        var minioBucketsJob = ReadRepoFile("deploy", "k8s", "platform", "stateful", "minio-buckets-job.yaml");

        Assert.Contains(
            "Storage__Endpoint: http://urfu-minio.urfu-platform.svc.cluster.local:9000",
            mediaValues,
            StringComparison.Ordinal);
        Assert.Contains("Storage__PublicEndpoint: https://storage.ghjc.ru", mediaValues, StringComparison.Ordinal);
        Assert.Contains("Storage__PrivateBucket: media-private", mediaValues, StringComparison.Ordinal);
        Assert.Contains("Storage__PublicBucket: media-public", mediaValues, StringComparison.Ordinal);
        Assert.Contains("host: storage.ghjc.ru", minioIngress, StringComparison.Ordinal);
        Assert.Contains("number: 9000", minioIngress, StringComparison.Ordinal);
        Assert.Contains("MINIO_API_CORS_ALLOW_ORIGIN", minioTenant, StringComparison.Ordinal);
        Assert.Contains("https://urfu-link.ghjc.ru", minioTenant, StringComparison.Ordinal);
        Assert.DoesNotContain("mc cors set", minioBucketsJob, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalKubernetesMediaStorageShouldUseClusterEndpointAndIngressHost()
    {
        var mediaValues = ReadRepoFile("deploy", "helm", "services", "media-service", "values-dev.yaml");
        var localSecrets = ReadRepoFile("platform", "dev", "local-k8s", "dev-secrets.yaml");
        var localIngress = ReadRepoFile("platform", "dev", "local-k8s", "local-ingress.yaml");
        var dependencies = ReadRepoFile("platform", "dev", "local-k8s", "dependencies.yaml");
        var mediaSecret = GetYamlDocumentByName(localSecrets, "media-service-secrets");

        Assert.Contains(
            "Storage__Endpoint: http://minio.urfu-platform.svc.cluster.local:9000",
            mediaValues,
            StringComparison.Ordinal);
        Assert.Contains(
            "Storage__PublicEndpoint: http://storage.dev.127.0.0.1.nip.io",
            mediaValues,
            StringComparison.Ordinal);
        Assert.Contains("Storage__AccessKey: minio", mediaSecret, StringComparison.Ordinal);
        Assert.Contains("Storage__SecretKey: minio123", mediaSecret, StringComparison.Ordinal);
        Assert.Contains("host: storage.dev.127.0.0.1.nip.io", localIngress, StringComparison.Ordinal);
        Assert.Contains("name: minio-bootstrap-buckets", dependencies, StringComparison.Ordinal);
        Assert.Contains("MINIO_API_CORS_ALLOW_ORIGIN", dependencies, StringComparison.Ordinal);
        Assert.Contains("http://localhost:3000", dependencies, StringComparison.Ordinal);
        Assert.Contains("http://app.dev.127.0.0.1.nip.io", dependencies, StringComparison.Ordinal);
        Assert.DoesNotContain("mc cors set", dependencies, StringComparison.Ordinal);
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
    public void PlatformStatefulShouldSourceMongoPasswordFromVaultExternalSecret()
    {
        var application = ReadRepoFile("deploy", "k8s", "platform", "argocd", "apps", "wave-5", "platform-stateful.yaml");
        var kustomization = ReadRepoFile("deploy", "k8s", "platform", "stateful", "kustomization.yaml");
        var externalSecret = ReadRepoFile(
            "deploy",
            "k8s",
            "platform",
            "stateful",
            "mongo-app-user-password-external-secret.yaml");
        var mongo = ReadRepoFile("deploy", "k8s", "platform", "stateful", "mongodb-cluster.yaml");

        Assert.DoesNotContain("name: mongo-app-user-password", application, StringComparison.Ordinal);
        Assert.Contains("mongo-app-user-password-external-secret.yaml", kustomization, StringComparison.Ordinal);
        Assert.Contains("kind: ExternalSecret", externalSecret, StringComparison.Ordinal);
        Assert.Contains("name: mongo-app-user-password", externalSecret, StringComparison.Ordinal);
        Assert.Contains("key: urfu-link/prod/mongo", externalSecret, StringComparison.Ordinal);
        Assert.Contains("property: app_user_password", externalSecret, StringComparison.Ordinal);
        Assert.DoesNotContain("PLACEHOLDER_WILL_BE_REPLACED_BY_BOOTSTRAP", mongo, StringComparison.Ordinal);
    }

    [Fact]
    public void MongoStatefulSetShouldStartAfterPlatformBootstrapGeneratesPassword()
    {
        var manifest = ReadRepoFile("deploy", "k8s", "platform", "stateful", "mongodb-cluster.yaml");

        Assert.Contains("name: mongo-app-user-password", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("kind: Secret", manifest, StringComparison.Ordinal);
        Assert.Contains("name: urfu-mongo", manifest, StringComparison.Ordinal);
        Assert.Contains("argocd.argoproj.io/sync-wave: \"4\"", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformBootstrapShouldProvisionPresencePostgresConnection()
    {
        var manifest = ReadRepoFile("deploy", "k8s", "platform", "stateful", "platform-bootstrap-job.yaml");

        Assert.Contains("presence_db_password", manifest, StringComparison.Ordinal);
        Assert.Contains("CREATE ROLE presence LOGIN PASSWORD '$PRESENCE_DB_PASSWORD'", manifest, StringComparison.Ordinal);
        Assert.Contains("CREATE DATABASE presence_db OWNER presence", manifest, StringComparison.Ordinal);
        Assert.Contains(
            "primary_connection\":\"Host=urfu-postgres-rw.urfu-platform.svc.cluster.local;Port=5432;Database=presence_db;Username=presence;Password=%s;GSS Encryption Mode=Disable",
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
            "primary_connection\":\"Host=urfu-postgres-rw.urfu-platform.svc.cluster.local;Port=5432;Database=notification_db;Username=notification;Password=%s;GSS Encryption Mode=Disable",
            bootstrap,
            StringComparison.Ordinal);

        var notificationSecretStart = externalSecrets.IndexOf("name: notification-service-secrets", StringComparison.Ordinal);
        var notificationSecretEnd = externalSecrets.IndexOf("---", notificationSecretStart, StringComparison.Ordinal);
        var notificationSecret = externalSecrets[notificationSecretStart..notificationSecretEnd];

        Assert.Contains("secretKey: ConnectionStrings__Primary", notificationSecret, StringComparison.Ordinal);
        Assert.Contains("key: urfu-link/prod/notification-service", notificationSecret, StringComparison.Ordinal);
        Assert.Contains("property: primary_connection", notificationSecret, StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformBootstrapShouldPersistMongoPasswordInVaultAndPreflightMongoAuth()
    {
        var bootstrap = ReadRepoFile("deploy", "k8s", "platform", "stateful", "platform-bootstrap-job.yaml");
        var preflight = ReadRepoFile("deploy", "k8s", "platform", "stateful", "mongodb-auth-preflight-job.yaml");
        var vault = ReadRepoFile("deploy", "k8s", "platform", "vault", "vault-auto-init-job.yaml");

        Assert.Contains("mongo_app_password", bootstrap, StringComparison.Ordinal);
        Assert.Contains("vault_put urfu-link/prod/mongo", bootstrap, StringComparison.Ordinal);
        Assert.Contains("app_user_password", bootstrap, StringComparison.Ordinal);
        Assert.Contains("path \"kv/data/urfu-link/prod/mongo\"", vault, StringComparison.Ordinal);
        Assert.Contains("kind: Job", preflight, StringComparison.Ordinal);
        Assert.Contains("name: mongodb-auth-preflight", preflight, StringComparison.Ordinal);
        Assert.Contains("mongosh \"$MONGO_URI\"", preflight, StringComparison.Ordinal);
    }

    [Fact]
    public void MinioShouldUseInternalKeycloakDiscoveryWithExplicitEgress()
    {
        var minio = ReadRepoFile("deploy", "k8s", "platform", "stateful", "minio-tenant.yaml");
        var policies = ReadRepoFile("deploy", "k8s", "platform", "network", "platform-network-policies.yaml");
        var coredns = ReadRepoFile("deploy", "k8s", "platform", "network", "coredns-custom.yaml");

        Assert.Contains(
            "MINIO_IDENTITY_OPENID_CONFIG_URL",
            minio,
            StringComparison.Ordinal);
        Assert.Contains(
            "http://keycloak.urfu-platform.svc.cluster.local:8080/realms/urfu-link/.well-known/openid-configuration",
            minio,
            StringComparison.Ordinal);
        Assert.Contains("name: minio-egress-keycloak", policies, StringComparison.Ordinal);
        Assert.Contains("app: keycloak", policies, StringComparison.Ordinal);
        Assert.Contains("port: 8080", policies, StringComparison.Ordinal);
        Assert.Contains("prefer internal service URLs", coredns, StringComparison.Ordinal);
    }

    [Fact]
    public void MongoBackupCronJobShouldUseHardenedSequentialOfficialImages()
    {
        var cronJob = ReadRepoFile("deploy", "k8s", "platform", "stateful", "mongodb-backup-cronjob.yaml");
        var smokeJob = ReadRepoFile("deploy", "k8s", "platform", "stateful", "mongodb-backup-smoke-job.yaml");
        var bucketsJob = ReadRepoFile("deploy", "k8s", "platform", "stateful", "minio-buckets-job.yaml");

        Assert.Contains("initContainers:", cronJob, StringComparison.Ordinal);
        Assert.Contains("name: mongodump", cronJob, StringComparison.Ordinal);
        Assert.Contains("image: mongo:8.0", cronJob, StringComparison.Ordinal);
        Assert.Contains("name: upload-to-minio", cronJob, StringComparison.Ordinal);
        Assert.Contains("image: minio/mc:RELEASE.2025-08-13T08-35-41Z", cronJob, StringComparison.Ordinal);
        Assert.Contains("concurrencyPolicy: Forbid", cronJob, StringComparison.Ordinal);
        Assert.Contains("activeDeadlineSeconds: 900", cronJob, StringComparison.Ordinal);
        Assert.Contains("successfulJobsHistoryLimit: 3", cronJob, StringComparison.Ordinal);
        Assert.Contains("failedJobsHistoryLimit: 3", cronJob, StringComparison.Ordinal);
        Assert.Contains("readOnlyRootFilesystem: true", cronJob, StringComparison.Ordinal);
        Assert.Contains("runAsNonRoot: true", cronJob, StringComparison.Ordinal);
        Assert.Contains("mongodump --uri \"$MONGO_URI\" --archive=\"$ARCHIVE\" --gzip", cronJob, StringComparison.Ordinal);
        Assert.Contains("OBJECT_PATH=\"urfu-backups/mongo", cronJob, StringComparison.Ordinal);
        Assert.Contains("mc stat minio/${OBJECT_PATH}", cronJob, StringComparison.Ordinal);
        Assert.Contains("mc mb -p minio/urfu-backups", bucketsJob, StringComparison.Ordinal);
        Assert.Contains("name: mongo-backup-smoke", smokeJob, StringComparison.Ordinal);
        Assert.Contains("initContainers:", smokeJob, StringComparison.Ordinal);
    }

    [Fact]
    public void KeycloakBootstrapShouldVerifyInternalGrpcTokenClaims()
    {
        var bootstrap = ReadRepoFile("deploy", "k8s", "platform", "identity", "keycloak-bootstrap-job.yaml");

        Assert.Contains("verify_internal_grpc_token", bootstrap, StringComparison.Ordinal);
        Assert.Contains("service:discipline-read", bootstrap, StringComparison.Ordinal);
        Assert.Contains("service:presence-internal", bootstrap, StringComparison.Ordinal);
        Assert.Contains("urfu-link-api", bootstrap, StringComparison.Ordinal);
        Assert.Contains("https://id.ghjc.ru/realms/urfu-link", bootstrap, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] pathSegments)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

        return File.ReadAllText(Path.Combine([repoRoot, .. pathSegments]));
    }

    private static string GetYamlDocumentByName(string manifest, string name)
    {
        var documentStart = manifest.IndexOf($"name: {name}", StringComparison.Ordinal);
        Assert.True(documentStart >= 0, $"Could not find YAML document named {name}.");

        documentStart = manifest.LastIndexOf("---", documentStart, StringComparison.Ordinal);
        documentStart = documentStart < 0 ? 0 : documentStart + "---".Length;

        var documentEnd = manifest.IndexOf("---", documentStart, StringComparison.Ordinal);
        return documentEnd < 0 ? manifest[documentStart..] : manifest[documentStart..documentEnd];
    }
}
