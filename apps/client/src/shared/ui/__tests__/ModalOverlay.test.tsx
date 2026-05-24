import { act, createRef } from "react";
import { render } from "@testing-library/react-native";
import { Text } from "react-native";

import { Menu, type MenuRef } from "../Menu";
import { ModalOverlay } from "../ModalOverlay";

const getClassNames = (root: ReturnType<typeof render>["UNSAFE_root"]) =>
    root
        .findAll((node) => typeof node.props.className === "string")
        .map((node) => node.props.className as string);

describe("modal cursor behavior", () => {
    it("keeps the modal backdrop and content surface on the default cursor", () => {
        const { UNSAFE_root } = render(
            <ModalOverlay
                visible
                onClose={jest.fn()}
                backdropClassName="px-4"
                contentClassName="custom-surface"
            >
                <Text>Modal content</Text>
            </ModalOverlay>,
        );

        const classNames = getClassNames(UNSAFE_root);
        const backdropClassName = classNames.find((className) =>
            className.includes("bg-black/60"),
        );
        const contentClassName = classNames.find((className) =>
            className.includes("custom-surface"),
        );

        expect(backdropClassName).toContain("cursor-default");
        expect(contentClassName).toContain("cursor-default");
    });

    it("keeps the menu backdrop and popover surface on the default cursor", () => {
        const ref = createRef<MenuRef>();

        const { UNSAFE_root } = render(
            <Menu
                ref={ref}
                model={[
                    {
                        label: "Профиль пользователя",
                        command: jest.fn(),
                    },
                ]}
            />,
        );

        act(() => {
            ref.current?.toggle();
        });

        const classNames = getClassNames(UNSAFE_root);
        const backdropClassName = classNames.find((className) =>
            className.includes("flex-1"),
        );
        const popoverClassName = classNames.find((className) =>
            className.includes("absolute top-16"),
        );

        expect(backdropClassName).toContain("cursor-default");
        expect(popoverClassName).toContain("cursor-default");
    });
});
