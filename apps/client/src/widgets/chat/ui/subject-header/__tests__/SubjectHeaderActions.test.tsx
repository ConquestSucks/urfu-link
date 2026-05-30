import { fireEvent, render, screen } from "@testing-library/react-native";

import { SubjectHeaderActions } from "../SubjectHeaderActions";

jest.mock("@/shared/ui", () => ({
    Menu: () => null,
}));

jest.mock("@/shared/ui/phosphor", () => {
    const makeIcon = (testID: string) => () => {
        const { Text } = require("react-native");
        return <Text testID={testID}>{testID}</Text>;
    };

    return {
        DotsThreeVerticalIcon: makeIcon("menu-icon"),
        MagnifyingGlassIcon: makeIcon("search-icon"),
        PhoneIcon: makeIcon("phone-icon"),
        VideoCameraIcon: makeIcon("video-icon"),
    };
});

jest.mock("../../../config/headerActions", () => ({
    getSubjectHeaderActions: () => [],
}));

describe("SubjectHeaderActions", () => {
    it("opens search from the subject header button", () => {
        const onOpenSearch = jest.fn();

        render(
            <SubjectHeaderActions
                canStartGroupAudioCall={false}
                canStartGroupVideoCall={false}
                onOpenSearch={onOpenSearch}
                onOpenMembers={jest.fn()}
                onOpenPinned={jest.fn()}
                notificationsMuted={false}
                onToggleNotifications={jest.fn()}
            />,
        );

        fireEvent.press(screen.getByTestId("subject-header-search"));

        expect(onOpenSearch).toHaveBeenCalledTimes(1);
    });
});
