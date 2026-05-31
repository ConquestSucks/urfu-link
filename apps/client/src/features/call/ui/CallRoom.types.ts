import type { CallSessionDto, CallTokenDto } from "@urfu-link/api-client";

export type ParticipantInfo = {
    userId: string;
    displayName: string;
    avatarUrl?: string | null;
    isConnected: boolean;
    isSelf: boolean;
};

export type CallRoomProps = {
    call: CallSessionDto;
    callToken: CallTokenDto | undefined;
    isTokenLoading: boolean;
    tokenError: string | null;
    loadError: string | null;
    isLeaving: boolean;
    isMobile: boolean;
    callTitle: string;
    conversationId: string;
    participantInfos: ParticipantInfo[];
    onLeave: () => void;
    onCloseError: () => void;
};
