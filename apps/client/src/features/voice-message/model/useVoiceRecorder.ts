import { useCallback, useEffect, useRef, useState } from "react";
import { Platform } from "react-native";
import {
    AudioModule,
    RecordingPresets,
    setAudioModeAsync,
    useAudioRecorder,
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
    const statusUrlRef = useRef<string | null>(null);
    const audioRecorder = useAudioRecorder(RecordingPresets.HIGH_QUALITY, (status) => {
        statusUrlRef.current = status.url;
        if (status.hasError) {
            setError(status.error ?? "Не удалось записать голосовое сообщение");
            setIsRecording(false);
        }
        if (status.isFinished) {
            setIsRecording(false);
        }
    });
    const [draft, setDraft] = useState<VoiceRecordingDraft | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [isRecording, setIsRecording] = useState(false);
    const [isStarting, setIsStarting] = useState(false);
    const [isStopping, setIsStopping] = useState(false);
    const [durationMillis, setDurationMillis] = useState(0);
    const durationMillisRef = useRef(0);
    const startedAtRef = useRef<number | null>(null);
    const isRecordingRef = useRef(false);

    useEffect(() => {
        isRecordingRef.current = isRecording;
    }, [isRecording]);

    useEffect(() => {
        durationMillisRef.current = durationMillis;
    }, [durationMillis]);

    useEffect(() => {
        if (!isRecording) return;

        const intervalId = setInterval(() => {
            if (startedAtRef.current === null) return;
            setDurationMillis(Date.now() - startedAtRef.current);
        }, 250);

        return () => clearInterval(intervalId);
    }, [isRecording]);

    const startRecording = useCallback(async () => {
        if (isRecording || isStarting || draft) return;

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
            startedAtRef.current = Date.now();
            durationMillisRef.current = 0;
            setDurationMillis(0);
            setIsRecording(true);
        } catch (recordingError) {
            console.error("Failed to start voice recording", recordingError);
            setError("Не удалось начать запись");
            startedAtRef.current = null;
            setIsRecording(false);
            await disableRecordingMode();
        } finally {
            setIsStarting(false);
        }
    }, [audioRecorder, draft, isRecording, isStarting]);

    const stopRecording = useCallback(async () => {
        if (!isRecording || isStopping) return null;

        setIsStopping(true);
        try {
            await audioRecorder.stop();
            setIsRecording(false);
            const elapsedMillis =
                startedAtRef.current === null
                    ? durationMillisRef.current
                    : Math.max(durationMillisRef.current, Date.now() - startedAtRef.current);
            durationMillisRef.current = elapsedMillis;
            setDurationMillis(elapsedMillis);
            startedAtRef.current = null;
            const uri = audioRecorder.uri ?? statusUrlRef.current;
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
            setIsRecording(false);
            setIsStopping(false);
            await disableRecordingMode();
        }
    }, [
        audioRecorder,
        isRecording,
        isStopping,
        maxDurationSeconds,
    ]);

    const cancelRecording = useCallback(async () => {
        setError(null);
        if (isRecording) {
            try {
                await audioRecorder.stop();
            } catch {
                // Ignore failed cancellation; the next prepareToRecordAsync recreates the recorder state.
            }
        }
        startedAtRef.current = null;
        durationMillisRef.current = 0;
        setDurationMillis(0);
        setIsRecording(false);
        setDraft(null);
        await disableRecordingMode();
    }, [audioRecorder, isRecording]);

    const discardDraft = useCallback(() => {
        setDraft(null);
        setError(null);
    }, []);

    useEffect(() => {
        if (!isRecording) return;
        if (durationMillis < maxDurationSeconds * 1000) return;
        void stopRecording();
    }, [
        durationMillis,
        isRecording,
        maxDurationSeconds,
        stopRecording,
    ]);

    useEffect(
        () => () => {
            if (isRecordingRef.current) {
                Promise.resolve(audioRecorder.stop()).catch(() => undefined);
            }
            void disableRecordingMode();
        },
        [audioRecorder],
    );

    return {
        draft,
        error,
        isRecording,
        isStarting,
        isStopping,
        durationSeconds: Math.floor(durationMillis / 1000),
        maxDurationSeconds,
        startRecording,
        stopRecording,
        cancelRecording,
        discardDraft,
    };
};
