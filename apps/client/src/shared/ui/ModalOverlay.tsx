import React from "react";
import { Modal, Pressable } from "react-native";

interface ModalOverlayProps {
  visible: boolean;
  onClose: () => void;
  children: React.ReactNode;
  /** Extra classes appended to the full-screen backdrop Pressable (e.g. "px-4") */
  backdropClassName?: string;
  /** Classes applied to the inner stop-propagation Pressable (the modal panel) */
  contentClassName?: string;
}

/**
 * Shared modal backdrop with "click outside to close" behaviour.
 *
 * Usage:
 * ```tsx
 * <ModalOverlay visible={isOpen} onClose={onClose} backdropClassName="px-4"
 *   contentClassName="bg-app-card border border-white/10 rounded-3xl overflow-hidden w-full max-w-[420px]">
 *   {children}
 * </ModalOverlay>
 * ```
 */
export const ModalOverlay = ({
  visible,
  onClose,
  children,
  backdropClassName,
  contentClassName,
}: ModalOverlayProps) => (
  <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
    <Pressable
      className={`flex-1 bg-black/60 justify-center items-center${backdropClassName ? ` ${backdropClassName}` : ""}`}
      onPress={onClose}
    >
      {/* Inner Pressable stops touch events from reaching the backdrop */}
      <Pressable onPress={(e) => e.stopPropagation()} className={contentClassName}>
        {children}
      </Pressable>
    </Pressable>
  </Modal>
);
