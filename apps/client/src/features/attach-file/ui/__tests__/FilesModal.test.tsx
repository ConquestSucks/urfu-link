import { fireEvent, render, screen } from "@testing-library/react-native";
import { Platform } from "react-native";
import type { ReactNode } from "react";
import type { DocumentPickerAsset } from "expo-document-picker";

import { FilesModal } from "../FilesModal";

let mockDropHandler: ((files: File[]) => void) | null = null;
let mockIsDragging = false;

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: () => ({ width: 1024, isDesktop: true, isMobile: false }),
}));

jest.mock("@/shared/lib/useWebFileDrop", () => ({
    useWebFileDrop: ({
        onFiles,
    }: {
        onFiles: (files: File[]) => void;
    }) => {
        mockDropHandler = onFiles;
        return { dropRef: jest.fn(), isDragging: mockIsDragging };
    },
}));

jest.mock("@/shared/ui/activity-indicator", () => ({
    ActivityIndicator: () => {
        const { Text } = require("react-native");
        return <Text testID="files-modal-submit-spinner">loading</Text>;
    },
}));

jest.mock("@/shared/ui/phosphor", () => {
    const { Text } = require("react-native");
    const Icon = ({ children }: { children?: ReactNode }) => <Text>{children}</Text>;

    return {
        FileIcon: Icon,
        ImageSquareIcon: Icon,
        UploadSimpleIcon: Icon,
        XIcon: Icon,
    };
});

const baseProps = {
    visible: true,
    onClose: jest.fn(),
    onPickFiles: jest.fn(),
    onAddDroppedFiles: jest.fn(),
    onRemove: jest.fn(),
    onSubmit: jest.fn(),
};

const attachment: DocumentPickerAsset = {
    name: "0x1900-000000-80-0-0.jpg",
    uri: "blob:test-image",
    size: 185 * 1024,
    mimeType: "image/jpeg",
    lastModified: 1,
};

const isDisabled = (node: ReturnType<typeof screen.getByTestId>) =>
    node.props.accessibilityState?.disabled ?? node.props.disabled;

describe("FilesModal", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockDropHandler = null;
        mockIsDragging = false;
        Object.defineProperty(Platform, "OS", { configurable: true, value: "web" });
    });

    it("renders the upload copy and selected file count", () => {
        render(<FilesModal {...baseProps} attachments={[attachment]} />);

        expect(screen.getByText("Загрузить файлы")).toBeTruthy();
        expect(screen.getByText("Перетащите файлы сюда")).toBeTruthy();
        expect(screen.getByText("или нажмите для выбора файлов")).toBeTruthy();
        expect(screen.getByText("Выбрано файлов: 1")).toBeTruthy();
        expect(screen.getByText(attachment.name)).toBeTruthy();
        expect(screen.getByText("185.0 KB")).toBeTruthy();
    });

    it("uses compact desktop dimensions", () => {
        render(<FilesModal {...baseProps} attachments={[attachment]} />);

        const panel = screen.getByTestId("files-modal-panel");
        const panelStyle = Array.isArray(panel?.props.style)
            ? Object.assign({}, ...panel!.props.style)
            : panel?.props.style;

        expect(panelStyle.width).toBe(660);
        expect(panelStyle.maxHeight).toBe("80%");
    });

    it("opens the document picker from the drop zone", () => {
        render(<FilesModal {...baseProps} attachments={[]} />);

        fireEvent.press(screen.getByTestId("files-modal-drop-zone"));

        expect(baseProps.onPickFiles).toHaveBeenCalled();
    });

    it("adds dropped files from the drop zone on web", () => {
        const dropped = { name: "dropped.pdf" } as File;
        render(<FilesModal {...baseProps} attachments={[]} />);

        mockDropHandler?.([dropped]);

        expect(baseProps.onAddDroppedFiles).toHaveBeenCalledWith([dropped]);
    });

    it("removes and submits selected files", () => {
        render(<FilesModal {...baseProps} attachments={[attachment]} />);

        fireEvent.press(screen.getByTestId("files-modal-remove-0"));
        fireEvent.press(screen.getByTestId("files-modal-submit"));

        expect(baseProps.onRemove).toHaveBeenCalledWith(0);
        expect(baseProps.onSubmit).toHaveBeenCalled();
    });

    it("disables submit when there are no selected files", () => {
        render(<FilesModal {...baseProps} attachments={[]} />);

        const submit = screen.getByTestId("files-modal-submit");

        expect(isDisabled(submit)).toBe(true);
        expect(baseProps.onSubmit).not.toHaveBeenCalled();
    });

    it("blocks controls and shows sending feedback while submitting", () => {
        render(
            <FilesModal
                {...baseProps}
                attachments={[attachment]}
                isSubmitting
                submitError="Не удалось отправить. Попробуйте ещё раз."
            />,
        );

        expect(screen.getByText("Загрузка файлов и отправка сообщения...")).toBeTruthy();
        expect(screen.getByText("Отправка...")).toBeTruthy();
        expect(screen.getByTestId("files-modal-submit-spinner")).toBeTruthy();
        expect(screen.getByText("Не удалось отправить. Попробуйте ещё раз.")).toBeTruthy();
        expect(isDisabled(screen.getByTestId("files-modal-submit"))).toBe(true);
        expect(isDisabled(screen.getByTestId("files-modal-remove-0"))).toBe(true);
    });
});
