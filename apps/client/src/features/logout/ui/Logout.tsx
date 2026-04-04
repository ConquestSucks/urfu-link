import { useWindowSize } from "@/shared/lib/useWindowSize";
import { Button } from "@/shared/ui";
import { SignOutIcon } from "@/shared/ui/phosphor";

export const Logout = () => {
    const { isMobile } = useWindowSize();
    return (
        <Button
            label="Выйти"
            onPress={() => {}}
            variant="danger"
            className={`
              justify-start
              ${isMobile ? "rounded-b-3xl p-5 gap-4" : "gap-3 px-[17.5px] py-[14px]"}
            `}
            textClassName={isMobile ? "text-base" : "text-[15px]"}
            icon={<SignOutIcon size={isMobile ? 22 : 20} className="text-danger-400" />}
        ></Button>
    );
};
