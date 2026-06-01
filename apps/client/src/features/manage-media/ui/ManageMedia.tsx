import { useCurrentUser, useUpdateSoundVideo } from "@/entities/user";
import { LabeledCard } from "@/shared/ui";
import { Select } from "@/shared/ui/Select";
import { Platform, ScrollView } from "react-native";
import {
    defaultDeviceValue,
    hasAudioOutputSelection,
    parseFacingModeValue,
    useMediaDevices,
} from "../lib/useMediaDevices";

export const ManageMedia = () => {
    const { data: profile } = useCurrentUser();
    const updateSoundVideo = useUpdateSoundVideo();

    const mic = useMediaDevices("audioinput", profile?.soundVideo.recordingDeviceId);
    const camera = useMediaDevices("videoinput", profile?.soundVideo.webcamDeviceId, {
        initialFacingMode: profile?.soundVideo.webcamFacingMode,
    });
    const speaker = useMediaDevices("audiooutput", profile?.soundVideo.playbackDeviceId);
    const canSelectSpeaker = Platform.OS !== "web" || hasAudioOutputSelection();

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
                        updateSoundVideo.mutate({
                            recordingDeviceId: val === defaultDeviceValue ? null : val as string,
                        });
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
                        const facingMode = parseFacingModeValue(val);
                        updateSoundVideo.mutate(
                            facingMode
                                ? { webcamDeviceId: null, webcamFacingMode: facingMode }
                                : { webcamDeviceId: val as string, webcamFacingMode: null },
                        );
                    }}
                    onOpen={camera.requestAccess}
                    emptyMessage={getEmptyMessage(camera.permissionStatus)}
                />
            </LabeledCard>

            {canSelectSpeaker ? (
                <LabeledCard label="Динамики">
                    <Select
                        placeholder="Вывод звука по умолчанию"
                        loading={speaker.isLoading}
                        options={speaker.options}
                        selectedValue={speaker.selectedValue}
                        onSelect={(val) => {
                            speaker.setSelectedValue(val);
                            updateSoundVideo.mutate({
                                playbackDeviceId: val === defaultDeviceValue ? null : val as string,
                            });
                        }}
                        onOpen={speaker.requestAccess}
                        emptyMessage={getEmptyMessage(speaker.permissionStatus)}
                    />
                </LabeledCard>
            ) : null}
        </ScrollView>
    );
};
