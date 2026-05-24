import { formatMessageDateLabel, getMessageDayKey } from "../message-date-labels";

describe("message date labels", () => {
    it("formats today as a relative label", () => {
        expect(
            formatMessageDateLabel(
                "2026-05-24T10:00:00.000Z",
                new Date("2026-05-24T12:00:00.000Z"),
            ),
        ).toBe("Сегодня");
    });

    it("formats older messages with month labels", () => {
        expect(
            formatMessageDateLabel(
                "2026-05-23T10:00:00.000Z",
                new Date("2026-05-24T12:00:00.000Z"),
            ),
        ).toContain("мая");
    });

    it("uses stable local day keys", () => {
        expect(getMessageDayKey("2026-05-23T10:00:00.000Z")).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    });
});
