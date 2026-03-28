import { LabeledCard, Select } from "@/shared/ui";
import { ScrollView } from "react-native";
import { useMediaDevices } from "../lib/useMediaDevices";

export const ManageMedia = () => {
  const mic = useMediaDevices("audioinput");
  const camera = useMediaDevices("videoinput");
  const speaker = useMediaDevices("audiooutput");

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
    <ScrollView
      contentContainerClassName="gap-4"
      showsVerticalScrollIndicator={false}
    >
      <LabeledCard label="Микрофон">
        <Select
          placeholder="Нажмите для выбора микрофона"
          loading={mic.isLoading}
          options={mic.options}
          selectedValue={mic.selectedValue}
          onSelect={mic.setSelectedValue}
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
          onSelect={camera.setSelectedValue}
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
          onSelect={speaker.setSelectedValue}
          onOpen={speaker.requestAccess}
          emptyMessage={getEmptyMessage(speaker.permissionStatus)}
        />
      </LabeledCard>
    </ScrollView>
  );
};
