import React from "react";
import { View } from "react-native";
import { Defs, LinearGradient, Path, Stop, Svg } from "react-native-svg";
export const Logo = ({ size }: {
    size: number;
}) => (<View style={{ width: size, height: size }}>
    <Svg width="100%" height="100%" viewBox="0 0 48 48" fill="none">
      <Path d="M24 45.6C35.9293 45.6 45.6 35.9294 45.6 24C45.6 12.0707 35.9293 2.40002 24 2.40002C12.0706 2.40002 2.39999 12.0707 2.39999 24C2.39999 35.9294 12.0706 45.6 24 45.6Z" stroke="url(#paint0_linear)" strokeWidth="2.4"/>
      <Path d="M15.6 22.8C17.5882 22.8 19.2 21.1882 19.2 19.2C19.2 17.2118 17.5882 15.6 15.6 15.6C13.6118 15.6 12 17.2118 12 19.2C12 21.1882 13.6118 22.8 15.6 22.8Z" fill="url(#paint1_linear)"/>
      <Path d="M32.4 22.8C34.3882 22.8 36 21.1882 36 19.2C36 17.2118 34.3882 15.6 32.4 15.6C30.4118 15.6 28.8 17.2118 28.8 19.2C28.8 21.1882 30.4118 22.8 32.4 22.8Z" fill="url(#paint2_linear)"/>
      <Path d="M24 34.8C25.9882 34.8 27.6 33.1882 27.6 31.2C27.6 29.2118 25.9882 27.6 24 27.6C22.0118 27.6 20.4 29.2118 22.0118 31.2C20.4 33.1882 22.0118 34.8 24 34.8Z" fill="url(#paint3_linear)"/>
      <Path d="M18 21L21.6 28.8" stroke="url(#paint4_linear)" strokeWidth="2.4" strokeLinecap="round"/>
      <Path d="M30 21L26.4 28.8" stroke="url(#paint5_linear)" strokeWidth="2.4" strokeLinecap="round"/>
      <Path d="M19.2 18H28.8" stroke="url(#paint6_linear)" strokeWidth="2.4" strokeLinecap="round"/>
      <Defs>
        <LinearGradient id="paint0_linear" x1="0" y1="0" x2="48" y2="48" gradientUnits="userSpaceOnUse">
          <Stop stopColor={"#3B82F6"}/>
          <Stop offset="1" stopColor={"#60A5FA"}/>
        </LinearGradient>
        <LinearGradient id="paint1_linear" x1="0" y1="0" x2="48" y2="48" gradientUnits="userSpaceOnUse">
          <Stop stopColor={"#3B82F6"}/>
          <Stop offset="1" stopColor={"#60A5FA"}/>
        </LinearGradient>
        <LinearGradient id="paint2_linear" x1="0" y1="0" x2="48" y2="48" gradientUnits="userSpaceOnUse">
          <Stop stopColor={"#3B82F6"}/>
          <Stop offset="1" stopColor={"#60A5FA"}/>
        </LinearGradient>
        <LinearGradient id="paint3_linear" x1="0" y1="0" x2="48" y2="48" gradientUnits="userSpaceOnUse">
          <Stop stopColor={"#3B82F6"}/>
          <Stop offset="1" stopColor={"#60A5FA"}/>
        </LinearGradient>
        <LinearGradient id="paint4_linear" x1="0" y1="0" x2="48" y2="48" gradientUnits="userSpaceOnUse">
          <Stop stopColor={"#3B82F6"}/>
          <Stop offset="1" stopColor={"#60A5FA"}/>
        </LinearGradient>
        <LinearGradient id="paint5_linear" x1="0" y1="0" x2="48" y2="48" gradientUnits="userSpaceOnUse">
          <Stop stopColor={"#3B82F6"}/>
          <Stop offset="1" stopColor={"#60A5FA"}/>
        </LinearGradient>
        <LinearGradient id="paint6_linear" x1="0" y1="0" x2="48" y2="48" gradientUnits="userSpaceOnUse">
          <Stop stopColor={"#3B82F6"}/>
          <Stop offset="1" stopColor={"#60A5FA"}/>
        </LinearGradient>
      </Defs>
    </Svg>
  </View>);
