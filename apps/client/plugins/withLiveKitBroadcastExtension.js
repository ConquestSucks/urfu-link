const fs = require("fs");
const path = require("path");
const { withDangerousMod, withXcodeProject } = require("expo/config-plugins");

const DEFAULT_EXTENSION_NAME = "URFULinkScreenShare";

const SAMPLE_HANDLER_SWIFT = `import CoreFoundation
import CoreImage
import CoreMedia
import CFNetwork
import Darwin
import ImageIO
import ReplayKit

private let socketFileName = "rtc_SSFD"

final class SampleHandler: RPBroadcastSampleHandler {
    private let context = CIContext()
    private let frameInterval = CMTime(value: 1, timescale: 15)
    private var lastFrameTime = CMTime.invalid
    private var socketFileDescriptor: Int32 = -1

    override func broadcastStarted(withSetupInfo setupInfo: [String: NSObject]?) {
        openSocket()
    }

    override func broadcastFinished() {
        closeSocket()
    }

    override func processSampleBuffer(
        _ sampleBuffer: CMSampleBuffer,
        with sampleBufferType: RPSampleBufferType
    ) {
        guard sampleBufferType == .video else {
            return
        }

        let timestamp = CMSampleBufferGetPresentationTimeStamp(sampleBuffer)
        if lastFrameTime.isValid {
            let elapsed = CMTimeSubtract(timestamp, lastFrameTime)
            if elapsed.seconds >= 0 && CMTimeCompare(elapsed, frameInterval) < 0 {
                return
            }
        }
        lastFrameTime = timestamp

        guard let imageBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else {
            return
        }

        writeFrame(imageBuffer, from: sampleBuffer)
    }

    private func openSocket() {
        guard socketFileDescriptor < 0, let socketPath else {
            return
        }

        let descriptor = Darwin.socket(AF_UNIX, SOCK_STREAM, 0)
        guard descriptor >= 0 else {
            return
        }

        var address = sockaddr_un()
        address.sun_family = sa_family_t(AF_UNIX)
        let maxPathLength = MemoryLayout.size(ofValue: address.sun_path)
        socketPath.withCString { source in
            withUnsafeMutablePointer(to: &address.sun_path.0) { destination in
                strncpy(destination, source, maxPathLength - 1)
            }
        }

        let result = withUnsafePointer(to: &address) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { socketAddress in
                Darwin.connect(
                    descriptor,
                    socketAddress,
                    socklen_t(MemoryLayout<sockaddr_un>.size)
                )
            }
        }

        if result == 0 {
            socketFileDescriptor = descriptor
        } else {
            Darwin.close(descriptor)
        }
    }

    private func closeSocket() {
        guard socketFileDescriptor >= 0 else {
            return
        }

        Darwin.close(socketFileDescriptor)
        socketFileDescriptor = -1
    }

    private var socketPath: String? {
        guard
            let appGroup = Bundle.main.object(forInfoDictionaryKey: "RTCAppGroupIdentifier") as? String,
            let container = FileManager.default.containerURL(
                forSecurityApplicationGroupIdentifier: appGroup
            )
        else {
            return nil
        }

        return container.appendingPathComponent(socketFileName).path
    }

    private func writeFrame(_ imageBuffer: CVImageBuffer, from sampleBuffer: CMSampleBuffer) {
        if socketFileDescriptor < 0 {
            openSocket()
        }
        guard socketFileDescriptor >= 0 else {
            return
        }

        let ciImage = CIImage(cvPixelBuffer: imageBuffer)
        let colorSpace = CGColorSpaceCreateDeviceRGB()
        guard
            let jpegData = context.jpegRepresentation(
                of: ciImage,
                colorSpace: colorSpace,
                options: [:]
            )
        else {
            return
        }

        let width = CVPixelBufferGetWidth(imageBuffer)
        let height = CVPixelBufferGetHeight(imageBuffer)
        let orientation = videoOrientation(from: sampleBuffer)

        guard
            let message = CFHTTPMessageCreateResponse(
                kCFAllocatorDefault,
                200,
                nil,
                kCFHTTPVersion1_1
            )?.takeRetainedValue()
        else {
            return
        }

        CFHTTPMessageSetHeaderFieldValue(message, "Content-Length" as CFString, "\\(jpegData.count)" as CFString)
        CFHTTPMessageSetHeaderFieldValue(message, "Buffer-Width" as CFString, "\\(width)" as CFString)
        CFHTTPMessageSetHeaderFieldValue(message, "Buffer-Height" as CFString, "\\(height)" as CFString)
        CFHTTPMessageSetHeaderFieldValue(message, "Buffer-Orientation" as CFString, "\\(orientation)" as CFString)
        CFHTTPMessageSetBody(message, jpegData as CFData)

        guard let serialized = CFHTTPMessageCopySerializedMessage(message)?.takeRetainedValue() as Data? else {
            return
        }

        write(serialized)
    }

    private func videoOrientation(from sampleBuffer: CMSampleBuffer) -> Int {
        let rawOrientation = CMGetAttachment(
            sampleBuffer,
            key: RPVideoSampleOrientationKey as CFString,
            attachmentModeOut: nil
        ) as? NSNumber

        return rawOrientation?.intValue ?? Int(CGImagePropertyOrientation.up.rawValue)
    }

    private func write(_ data: Data) {
        data.withUnsafeBytes { buffer in
            guard let baseAddress = buffer.baseAddress else {
                return
            }

            var offset = 0
            while offset < data.count {
                let written = Darwin.write(
                    socketFileDescriptor,
                    baseAddress.advanced(by: offset),
                    data.count - offset
                )

                if written <= 0 {
                    closeSocket()
                    return
                }

                offset += written
            }
        }
    }
}
`;

function createInfoPlist({ extensionBundleIdentifier, appGroupIdentifier }) {
    return `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>$(DEVELOPMENT_LANGUAGE)</string>
  <key>CFBundleDisplayName</key>
  <string>URFU Link Screen Share</string>
  <key>CFBundleExecutable</key>
  <string>$(EXECUTABLE_NAME)</string>
  <key>CFBundleIdentifier</key>
  <string>${extensionBundleIdentifier}</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>$(PRODUCT_NAME)</string>
  <key>CFBundlePackageType</key>
  <string>$(PRODUCT_BUNDLE_PACKAGE_TYPE)</string>
  <key>CFBundleShortVersionString</key>
  <string>$(MARKETING_VERSION)</string>
  <key>CFBundleVersion</key>
  <string>$(CURRENT_PROJECT_VERSION)</string>
  <key>NSExtension</key>
  <dict>
    <key>NSExtensionPointIdentifier</key>
    <string>com.apple.broadcast-services-upload</string>
    <key>NSExtensionPrincipalClass</key>
    <string>$(PRODUCT_MODULE_NAME).SampleHandler</string>
  </dict>
  <key>RTCAppGroupIdentifier</key>
  <string>${appGroupIdentifier}</string>
</dict>
</plist>
`;
}

function createEntitlementsPlist(appGroupIdentifier) {
    return `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>com.apple.security.application-groups</key>
  <array>
    <string>${appGroupIdentifier}</string>
  </array>
</dict>
</plist>
`;
}

function getOptions(config, options = {}) {
    const iosBundleIdentifier =
        options.iosBundleIdentifier ??
        config.ios?.bundleIdentifier ??
        "ru.urfu.link";
    const extensionName = options.extensionName ?? DEFAULT_EXTENSION_NAME;
    const extensionBundleIdentifier =
        options.extensionBundleIdentifier ?? `${iosBundleIdentifier}.screenshare`;
    const appGroupIdentifier =
        options.appGroupIdentifier ?? `group.${iosBundleIdentifier}.screenshare`;

    return {
        appGroupIdentifier,
        extensionBundleIdentifier,
        extensionName,
    };
}

function ensureBuildConfiguration(project, targetUuid, settings) {
    const target = project.pbxNativeTargetSection()[targetUuid];
    if (!target?.buildConfigurationList) {
        return;
    }

    const configurationList =
        project.pbxXCConfigurationList()[target.buildConfigurationList];
    const configurationIds =
        configurationList?.buildConfigurations?.map((item) => item.value) ?? [];
    const buildConfigurations = project.pbxXCBuildConfigurationSection();

    for (const configurationId of configurationIds) {
        const configuration = buildConfigurations[configurationId];
        if (!configuration?.buildSettings) {
            continue;
        }

        configuration.buildSettings = {
            ...configuration.buildSettings,
            ...settings,
        };
    }
}

function findTargetUuid(project, targetName) {
    const nativeTargets = project.pbxNativeTargetSection();
    return Object.entries(nativeTargets).find(([, target]) => {
        return target?.name === `"${targetName}"` || target?.name === targetName;
    })?.[0];
}

function ensureBuildPhase(project, targetUuid, phaseType, name, files = []) {
    const target = project.pbxNativeTargetSection()[targetUuid];
    const buildPhases = target?.buildPhases ?? [];
    const existing = buildPhases.find((phase) => phase.comment === name);
    if (existing) {
        return;
    }

    project.addBuildPhase(files, phaseType, name, targetUuid);
}

function withLiveKitBroadcastExtension(config, options) {
    const resolved = getOptions(config, options);

    config = withDangerousMod(config, [
        "ios",
        async (modConfig) => {
            const extensionDir = path.join(
                modConfig.modRequest.platformProjectRoot,
                resolved.extensionName,
            );

            fs.mkdirSync(extensionDir, { recursive: true });
            fs.writeFileSync(
                path.join(extensionDir, "SampleHandler.swift"),
                SAMPLE_HANDLER_SWIFT,
            );
            fs.writeFileSync(
                path.join(extensionDir, "Info.plist"),
                createInfoPlist(resolved),
            );
            fs.writeFileSync(
                path.join(extensionDir, `${resolved.extensionName}.entitlements`),
                createEntitlementsPlist(resolved.appGroupIdentifier),
            );

            return modConfig;
        },
    ]);

    return withXcodeProject(config, (modConfig) => {
        const project = modConfig.modResults;
        const existingTargetUuid = findTargetUuid(project, resolved.extensionName);
        const targetUuid =
            existingTargetUuid ??
            project.addTarget(
                resolved.extensionName,
                "app_extension",
                resolved.extensionName,
                resolved.extensionBundleIdentifier,
            ).uuid;

        ensureBuildPhase(project, targetUuid, "PBXSourcesBuildPhase", "Sources", [
            `${resolved.extensionName}/SampleHandler.swift`,
        ]);
        ensureBuildPhase(project, targetUuid, "PBXFrameworksBuildPhase", "Frameworks");

        ensureBuildConfiguration(project, targetUuid, {
            APPLICATION_EXTENSION_API_ONLY: "YES",
            CODE_SIGN_ENTITLEMENTS: `${resolved.extensionName}/${resolved.extensionName}.entitlements`,
            CURRENT_PROJECT_VERSION: "$(CURRENT_PROJECT_VERSION)",
            DEVELOPMENT_TEAM: "$(DEVELOPMENT_TEAM)",
            INFOPLIST_FILE: `${resolved.extensionName}/Info.plist`,
            IPHONEOS_DEPLOYMENT_TARGET: "15.1",
            MARKETING_VERSION: "$(MARKETING_VERSION)",
            PRODUCT_BUNDLE_IDENTIFIER: resolved.extensionBundleIdentifier,
            PRODUCT_NAME: `"${resolved.extensionName}"`,
            SKIP_INSTALL: "YES",
            SWIFT_VERSION: "5.0",
            TARGETED_DEVICE_FAMILY: '"1,2"',
        });

        return modConfig;
    });
}

module.exports = withLiveKitBroadcastExtension;
