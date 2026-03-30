import { cssInterop } from "nativewind";
import type { ComponentType } from "react";
import * as Phosphor from "phosphor-react-native";
import type { IconProps } from "./phosphor-types";

export type { IconProps } from "./phosphor-types";

const iconInterop = {
  className: {
    target: "style" as const,
    nativeStyleToProp: {
      color: true as const,
    },
  },
} as const;

const wrap = (Base: Parameters<typeof cssInterop>[0]) =>
  cssInterop(Base, iconInterop as never) as ComponentType<IconProps>;

const iconBases = {
  AtIcon: Phosphor.AtIcon,
  BellIcon: Phosphor.BellIcon,
  BellSlashIcon: Phosphor.BellSlashIcon,
  BookOpenIcon: Phosphor.BookOpenIcon,
  CaretDownIcon: Phosphor.CaretDownIcon,
  CaretLeftIcon: Phosphor.CaretLeftIcon,
  CaretRightIcon: Phosphor.CaretRightIcon,
  ChatCircleTextIcon: Phosphor.ChatCircleTextIcon,
  CheckIcon: Phosphor.CheckIcon,
  ChecksIcon: Phosphor.ChecksIcon,
  DeviceMobileIcon: Phosphor.DeviceMobileIcon,
  DotsThreeVerticalIcon: Phosphor.DotsThreeVerticalIcon,
  EnvelopeIcon: Phosphor.EnvelopeIcon,
  EnvelopeSimpleIcon: Phosphor.EnvelopeSimpleIcon,
  FileIcon: Phosphor.FileIcon,
  GearIcon: Phosphor.GearIcon,
  InfoIcon: Phosphor.InfoIcon,
  LaptopIcon: Phosphor.LaptopIcon,
  LockIcon: Phosphor.LockIcon,
  MagnifyingGlassIcon: Phosphor.MagnifyingGlassIcon,
  MonitorIcon: Phosphor.MonitorIcon,
  PaperPlaneRightIcon: Phosphor.PaperPlaneRightIcon,
  PhoneIcon: Phosphor.PhoneIcon,
  PlusCircleIcon: Phosphor.PlusCircleIcon,
  PlusIcon: Phosphor.PlusIcon,
  ProhibitIcon: Phosphor.ProhibitIcon,
  PushPinIcon: Phosphor.PushPinIcon,
  ShieldCheckIcon: Phosphor.ShieldCheckIcon,
  SignOutIcon: Phosphor.SignOutIcon,
  SmileyIcon: Phosphor.SmileyIcon,
  SpeakerHighIcon: Phosphor.SpeakerHighIcon,
  TrashIcon: Phosphor.TrashIcon,
  UserCircleIcon: Phosphor.UserCircleIcon,
  UserIcon: Phosphor.UserIcon,
  UsersIcon: Phosphor.UsersIcon,
  VideoCameraIcon: Phosphor.VideoCameraIcon,
  XIcon: Phosphor.XIcon,
} as const;

const wrappedIcons = Object.fromEntries(
  (Object.entries(iconBases) as [keyof typeof iconBases, (typeof iconBases)[keyof typeof iconBases]][]).map(
    ([name, Base]) => [name, wrap(Base)],
  ),
) as { [K in keyof typeof iconBases]: ComponentType<IconProps> };

export const {
  AtIcon,
  BellIcon,
  BellSlashIcon,
  BookOpenIcon,
  CaretDownIcon,
  CaretLeftIcon,
  CaretRightIcon,
  ChatCircleTextIcon,
  CheckIcon,
  ChecksIcon,
  DeviceMobileIcon,
  DotsThreeVerticalIcon,
  EnvelopeIcon,
  EnvelopeSimpleIcon,
  FileIcon,
  GearIcon,
  InfoIcon,
  LaptopIcon,
  LockIcon,
  MagnifyingGlassIcon,
  MonitorIcon,
  PaperPlaneRightIcon,
  PhoneIcon,
  PlusCircleIcon,
  PlusIcon,
  ProhibitIcon,
  PushPinIcon,
  ShieldCheckIcon,
  SignOutIcon,
  SmileyIcon,
  SpeakerHighIcon,
  TrashIcon,
  UserCircleIcon,
  UserIcon,
  UsersIcon,
  VideoCameraIcon,
  XIcon,
} = wrappedIcons;
