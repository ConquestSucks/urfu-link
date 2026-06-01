import { render, screen } from "@testing-library/react-native";
import { SearchBar } from "../SearchBar";

describe("SearchBar native styling", () => {
    it("keeps the native text input background transparent", () => {
        render(<SearchBar placeholder="Search" value="" onChange={jest.fn()} />);

        const input = screen.getByPlaceholderText("Search");

        expect(input.props.className).not.toContain("selection:bg");
        expect(input.props.className).not.toContain("caret-");
        expect(input.props.placeholderTextColor).toBe("#8B8FA8");
        expect(input.props.selectionColor).toBe("#2B7FFF");
        expect(input.props.cursorColor).toBe("#8B8FA8");
        expect(input.props.style).toEqual(
            expect.objectContaining({
                backgroundColor: "transparent",
            }),
        );
    });
});
