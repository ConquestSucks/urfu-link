import { useCallback, useEffect, useRef, useState } from "react";
import { getVoiceFileName } from "../lib/format";
import type { VoiceRecordingDraft } from "./types";

const DEFAULT_MAX_DURATION_SECONDS = 300;
const RECORDER_MIME_TYPES = ["audio/webm;codecs=opus", "audio/webm", "audio/ogg;codecs=opus"];

type UseVoiceRecorderOptions = {
    maxDurationSeconds?: number;
};

const getSupportedMimeType = () => {
    if (typeof MediaRecorder === "undefined") return "";
    return RECORDER_MIME_TYPES.find((mimeType) => MediaRecorder.isTypeSupported(mimeType)) ?? "";
};

const stopMediaStream = (stream: MediaStream | null) => {
    stream?.getTracks().forEach((track) => track.stop());
};

const revokeObjectUrl = (url: string | null) => {
    if (url?.startsWith("blob:")) {
        URL.revokeObjectURL(url);
    }
};

export const useVoiceRecorder = ({
    maxDurationSeconds = DEFAULT_MAX_DURATION_SECONDS,
}: UseVoiceRecorderOptions = {}) => {
    const [draft, setDraft] = useState<VoiceRecordingDraft | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [isRecording, setIsRecording] = useState(false);
    const [isStarting, setIsStarting] = useState(false);
    const [isStopping, setIsStopping] = useState(false);
    const [durationSeconds, setDurationSeconds] = useState(0);
    const recorderRef = useRef<MediaRecorder | null>(null);
    const streamRef = useRef<MediaStream | null>(null);
    const chunksRef = useRef<BlobPart[]>([]);
    const startedAtRef = useRef(0);
    const durationTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
    const stopResolverRef = useRef<((draft: VoiceRecordingDraft | null) => void) | null>(null);
    const discardStopRef = useRef(false);
    const draftUriRef = useRef<string | null>(null);

    const clearDurationTimer = useCallback(() => {
        if (!durationTimerRef.current) return;
        clearInterval(durationTimerRef.current);
        durationTimerRef.current = null;
    }, []);

    const clearRecorder = useCallback(() => {
        clearDurationTimer();
        stopMediaStream(streamRef.current);
        streamRef.current = null;
        recorderRef.current = null;
    }, [clearDurationTimer]);

    const startRecording = useCallback(async () => {
        if (isRecording || isStarting || draft) return;

        if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === "undefined") {
            setError("Voice recording is not supported in this browser");
            return;
        }

        setError(null);
        setIsStarting(true);
        discardStopRef.current = false;

        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            const mimeType = getSupportedMimeType();
            const recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);

            streamRef.current = stream;
            recorderRef.current = recorder;
            chunksRef.current = [];
            startedAtRef.current = Date.now();
            setDurationSeconds(0);

            recorder.ondataavailable = (event) => {
                if (event.data.size > 0) {
                    chunksRef.current.push(event.data);
                }
            };

            recorder.onerror = () => {
                setError("Voice recording failed");
            };

            recorder.onstop = () => {
                const elapsedSeconds = Math.max(
                    1,
                    Math.min(maxDurationSeconds, Math.floor((Date.now() - startedAtRef.current) / 1000)),
                );
                const shouldDiscard = discardStopRef.current;
                discardStopRef.current = false;

                let nextDraft: VoiceRecordingDraft | null = null;
                if (!shouldDiscard && chunksRef.current.length > 0) {
                    const recordedMimeType = recorder.mimeType || "audio/webm";
                    const blob = new Blob(chunksRef.current, { type: recordedMimeType });
                    const uri = URL.createObjectURL(blob);
                    nextDraft = {
                        uri,
                        fileName: getVoiceFileName(recordedMimeType),
                        mimeType: recordedMimeType,
                        durationSeconds: elapsedSeconds,
                    };
                    setDraft((previousDraft) => {
                        revokeObjectUrl(previousDraft?.uri ?? null);
                        draftUriRef.current = nextDraft?.uri ?? null;
                        return nextDraft;
                    });
                }

                chunksRef.current = [];
                clearRecorder();
                setIsRecording(false);
                setIsStopping(false);
                setDurationSeconds(0);
                stopResolverRef.current?.(nextDraft);
                stopResolverRef.current = null;
            };

            recorder.start();
            setIsRecording(true);
            durationTimerRef.current = setInterval(() => {
                const nextSeconds = Math.floor((Date.now() - startedAtRef.current) / 1000);
                setDurationSeconds(Math.min(maxDurationSeconds, nextSeconds));
            }, 250);
        } catch (recordingError) {
            console.error("Failed to start voice recording", recordingError);
            setError("Microphone access was denied");
            clearRecorder();
        } finally {
            setIsStarting(false);
        }
    }, [clearRecorder, draft, isRecording, isStarting, maxDurationSeconds]);

    const stopRecording = useCallback(async () => {
        const recorder = recorderRef.current;
        if (!recorder || recorder.state === "inactive" || isStopping) return null;

        setIsStopping(true);
        return new Promise<VoiceRecordingDraft | null>((resolve) => {
            stopResolverRef.current = resolve;
            recorder.stop();
        });
    }, [isStopping]);

    const cancelRecording = useCallback(async () => {
        setError(null);
        const recorder = recorderRef.current;
        if (recorder && recorder.state !== "inactive") {
            discardStopRef.current = true;
            await new Promise<void>((resolve) => {
                stopResolverRef.current = () => resolve();
                recorder.stop();
            });
            return;
        }

        clearRecorder();
        setIsRecording(false);
        setIsStopping(false);
        setDurationSeconds(0);
    }, [clearRecorder]);

    const discardDraft = useCallback(() => {
        setDraft((previousDraft) => {
            revokeObjectUrl(previousDraft?.uri ?? null);
            draftUriRef.current = null;
            return null;
        });
        setError(null);
    }, []);

    useEffect(() => {
        if (!isRecording || durationSeconds < maxDurationSeconds) return;
        void stopRecording();
    }, [durationSeconds, isRecording, maxDurationSeconds, stopRecording]);

    useEffect(
        () => () => {
            stopResolverRef.current?.(null);
            clearRecorder();
            revokeObjectUrl(draftUriRef.current);
            draftUriRef.current = null;
        },
        [clearRecorder],
    );

    return {
        draft,
        error,
        isRecording,
        isStarting,
        isStopping,
        durationSeconds,
        maxDurationSeconds,
        startRecording,
        stopRecording,
        cancelRecording,
        discardDraft,
    };
};
