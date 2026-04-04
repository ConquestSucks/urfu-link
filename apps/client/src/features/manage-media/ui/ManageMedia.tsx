import { useCurrentUser, useUpdateSoundVideo } from "@/entities/user";
import { LabeledCard, Select } from "@/shared/ui";
import { ScrollView } from "react-native";
import { useMediaDevices } from "../lib/useMediaDevices";

export const ManageMedia = () => {
    const { data: profile } = useCurrentUser();
    const updateSoundVideo = useUpdateSoundVideo();

    const mic = useMediaDevices("audioinput", profile?.soundVideo.recordingDeviceId);
    const camera = useMediaDevices("videoinput", profile?.soundVideo.webcamDeviceId);
    const speaker = useMediaDevices("audiooutput", profile?.soundVideo.playbackDeviceId);

    const getEmptyMessage = (status: string) => {
        switch (status) {
            case "denied":
                return "Пожалуйста, предоставьте доступ в браузере";
            case "not_found":
                return "Устройство не подключено";
            case "busy":
                return "Камера занята другим приложением или выключена";
            case "error":
                return "Неизвестная ошибка устройства";
            default:
                return "Нет доступных устройств";
        }
    };

    return (
        <ScrollView contentContainerClassName="gap-4" showsVerticalScrollIndicator={false}>
            <LabeledCard label="Микрофон">
                <Select
                    placeholder="Нажмите для выбора микрофона"
                    loading={mic.isLoading}
                    options={mic.options}
                    selectedValue={mic.selectedValue}
                    onSelect={(val) => {
                        mic.setSelectedValue(val);
                        updateSoundVideo.mutate({ recordingDeviceId: val as string });
                    }}
                    onOpen={mic.requestAccess}
                    emptyMessage={getEmptyMessage(mic.permissionStatus)}
                />
            </LabeledCard>

            <LabeledCard label="Камера">
                <Select
                    placeholder="Нажмите для выбора камеры"
                    loading={camera.isLoading}
                    options={camera.options}
                    selectedValue={camera.selectedValue}
                    onSelect={(val) => {
                        camera.setSelectedValue(val);
                        updateSoundVideo.mutate({ webcamDeviceId: val as string });
                    }}
                    onOpen={camera.requestAccess}
                    emptyMessage={getEmptyMessage(camera.permissionStatus)}
                />
            </LabeledCard>

            <LabeledCard label="Динамики">
                <Select
                    placeholder="Вывод звука по умолчанию"
                    loading={speaker.isLoading}
                    options={speaker.options}
                    selectedValue={speaker.selectedValue}
                    onSelect={(val) => {
                        speaker.setSelectedValue(val);
                        updateSoundVideo.mutate({ playbackDeviceId: val as string });
                    }}
                    onOpen={speaker.requestAccess}
                    emptyMessage={getEmptyMessage(speaker.permissionStatus)}
                />
            </LabeledCard>
        </ScrollView>
    );
};
