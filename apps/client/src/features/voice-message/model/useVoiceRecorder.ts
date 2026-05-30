import { useCallback, useEffect, useRef, useState } from "react";
import { Platform } from "react-native";
import {
    AudioModule,
    RecordingPresets,
    setAudioModeAsync,
    useAudioRecorder,
    useAudioRecorderState,
} from "expo-audio";
import { getVoiceFileName, getVoiceMimeType } from "../lib/format";
import type { VoiceRecordingDraft } from "./types";

const DEFAULT_MAX_DURATION_SECONDS = 300;

const disableRecordingMode = () =>
    setAudioModeAsync({
        allowsRecording: false,
        playsInSilentMode: true,
        shouldPlayInBackground: false,
        shouldRouteThroughEarpiece: false,
    }).catch(() => undefined);

type UseVoiceRecorderOptions = {
    maxDurationSeconds?: number;
};

export const useVoiceRecorder = ({
    maxDurationSeconds = DEFAULT_MAX_DURATION_SECONDS,
}: UseVoiceRecorderOptions = {}) => {
    const audioRecorder = useAudioRecorder(RecordingPresets.HIGH_QUALITY);
    const recorderState = useAudioRecorderState(audioRecorder);
    const [draft, setDraft] = useState<VoiceRecordingDraft | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [isStarting, setIsStarting] = useState(false);
    const [isStopping, setIsStopping] = useState(false);
    const durationMillisRef = useRef(0);

    useEffect(() => {
        durationMillisRef.current = recorderState.durationMillis ?? 0;
    }, [recorderState.durationMillis]);

    const startRecording = useCallback(async () => {
        if (recorderState.isRecording || isStarting || draft) return;

        setError(null);
        setIsStarting(true);
        try {
            const permission = await AudioModule.requestRecordingPermissionsAsync();
            if (!permission.granted) {
                setError("Доступ к микрофону запрещён");
                return;
            }

            await setAudioModeAsync({
                allowsRecording: true,
                playsInSilentMode: true,
                shouldPlayInBackground: false,
                shouldRouteThroughEarpiece: false,
            });
            await audioRecorder.prepareToRecordAsync();
            await Promise.resolve(audioRecorder.record());
        } catch (recordingError) {
            console.error("Failed to start voice recording", recordingError);
            setError("Не удалось начать запись");
            await disableRecordingMode();
        } finally {
            setIsStarting(false);
        }
    }, [audioRecorder, draft, isStarting, recorderState.isRecording]);

    const stopRecording = useCallback(async () => {
        if (!recorderState.isRecording || isStopping) return null;

        setIsStopping(true);
        try {
            await audioRecorder.stop();
            const uri = audioRecorder.uri ?? recorderState.url;
            if (!uri) {
                setError("Не удалось сохранить запись");
                return null;
            }

            const durationSeconds = Math.max(
                1,
                Math.min(maxDurationSeconds, Math.floor(durationMillisRef.current / 1000)),
            );
            const mimeType = getVoiceMimeType(uri, Platform.OS === "web" ? "web" : "native");
            const nextDraft: VoiceRecordingDraft = {
                uri,
                fileName: getVoiceFileName(mimeType),
                mimeType,
                durationSeconds,
            };
            setDraft(nextDraft);
            return nextDraft;
        } catch (stopError) {
            console.error("Failed to stop voice recording", stopError);
            setError("Не удалось остановить запись");
            return null;
        } finally {
            setIsStopping(false);
            await disableRecordingMode();
        }
    }, [
        audioRecorder,
        isStopping,
        maxDurationSeconds,
        recorderState.isRecording,
        recorderState.url,
    ]);

    const cancelRecording = useCallback(async () => {
        setError(null);
        if (recorderState.isRecording) {
            try {
                await audioRecorder.stop();
            } catch {
                // Ignore failed cancellation; the next prepareToRecordAsync recreates the recorder state.
            }
        }
        setDraft(null);
        await disableRecordingMode();
    }, [audioRecorder, recorderState.isRecording]);

    const discardDraft = useCallback(() => {
        setDraft(null);
        setError(null);
    }, []);

    useEffect(() => {
        if (!recorderState.isRecording) return;
        if ((recorderState.durationMillis ?? 0) < maxDurationSeconds * 1000) return;
        void stopRecording();
    }, [
        maxDurationSeconds,
        recorderState.durationMillis,
        recorderState.isRecording,
        stopRecording,
    ]);

    useEffect(
        () => () => {
            if (audioRecorder.isRecording) {
                Promise.resolve(audioRecorder.stop()).catch(() => undefined);
            }
            void disableRecordingMode();
        },
        [audioRecorder],
    );

    return {
        draft,
        error,
        isRecording: recorderState.isRecording,
        isStarting,
        isStopping,
        durationSeconds: Math.floor((recorderState.durationMillis ?? 0) / 1000),
        maxDurationSeconds,
        startRecording,
        stopRecording,
        cancelRecording,
        discardDraft,
    };
};
