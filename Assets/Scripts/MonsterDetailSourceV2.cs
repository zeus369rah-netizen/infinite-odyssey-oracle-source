using System;
using UnityEngine;
using Style=OdysseyOracle.MonsterVoxelCosmetics.Style;
using PartSpec=OdysseyOracle.MonsterVoxelCosmetics.PartSpec;
using BoxSpec=OdysseyOracle.MonsterVoxelCosmetics.BoxSpec;
namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Versioned literal copy of the pinned V1 source table, not a reflection or
    // private runtime call. Original identity, pivots and boxes remain exact.
    internal static class MonsterDetailSourceV2
    {
        internal static PartSpec[] Build(Style style)
        {
            switch(style)
            {
                case Style.GuardianGolem: return new PartSpec[] {
                    new PartSpec("Torso",0,0,new Vector3(0f,1.04f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.03f,0f),new Vector3(0.82f,0.68f,0.49f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.35f,-0.015f),new Vector3(1.04f,0.2f,0.53f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.46f,1.36f,0.015f),new Vector3(0.27f,0.24f,0.55f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.46f,1.36f,0.015f),new Vector3(0.27f,0.24f,0.55f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,0.82f,0.275f),new Vector3(0.54f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Stone brow",0,0,new Vector3(0f,1.68f,-0.015f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.71f,-0.05f),new Vector3(0.64f,0.38f,0.47f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.86f,0.06f),new Vector3(0.7f,0.09f,0.5f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.255f,1.61f,0.22f),new Vector3(0.1f,0.22f,0.12f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.255f,1.61f,0.22f),new Vector3(0.1f,0.22f,0.12f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Face shadow",0,1,new Vector3(0f,1.65f,0.258f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.65f,0.258f),new Vector3(0.46f,0.18f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left rune eye",0,2,new Vector3(-0.13f,1.67f,0.282f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.13f,1.67f,0.282f),new Vector3(0.1f,0.07f,0.025f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right rune eye",0,2,new Vector3(0.13f,1.67f,0.282f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.13f,1.67f,0.282f),new Vector3(0.1f,0.07f,0.025f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Chest rune",0,2,new Vector3(0f,1.12f,0.257f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.12f,0.257f),new Vector3(0.16f,0.24f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Hip core",0,1,new Vector3(0f,0.58f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.58f,0f),new Vector3(0.54f,0.2f,0.34f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left leg",0,0,new Vector3(-0.23f,0.29f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.23f,0.44f,0f),new Vector3(0.34f,0.24f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.18f,-0.015f),new Vector3(0.3128f,0.23f,0.29f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.045f,0.07f),new Vector3(0.34f,0.09f,0.43f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.3f,0.185f),new Vector3(0.2652f,0.16f,0.085f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.068f,0.235f),new Vector3(0.2788f,0.045f,0.045f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left stone arm",1,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.15f,0.015f),new Vector3(0.34f,0.3f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.435f,0.025f),new Vector3(0.3128f,0.22f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.06f),new Vector3(0.34f,0.14f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.1f,0.205f),new Vector3(0.2822f,0.2f,0.07f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.26f),new Vector3(0.272f,0.1f,0.08f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right leg",0,0,new Vector3(0.23f,0.29f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.23f,0.44f,0f),new Vector3(0.34f,0.24f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.18f,-0.015f),new Vector3(0.3128f,0.23f,0.29f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.045f,0.07f),new Vector3(0.34f,0.09f,0.43f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.3f,0.185f),new Vector3(0.2652f,0.16f,0.085f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.068f,0.235f),new Vector3(0.2788f,0.045f,0.045f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right stone arm",2,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.15f,0.015f),new Vector3(0.34f,0.3f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.435f,0.025f),new Vector3(0.3128f,0.22f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.06f),new Vector3(0.34f,0.14f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.1f,0.205f),new Vector3(0.2822f,0.2f,0.07f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.26f),new Vector3(0.272f,0.1f,0.08f),new Vector3(0f,0f,0f)),
                    }),
                };
                case Style.Stalker: return new PartSpec[] {
                    new PartSpec("Torso",0,0,new Vector3(0f,1.04f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.05f,-0.055f),new Vector3(0.6f,0.66f,0.36f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.31f,-0.07f),new Vector3(0.78f,0.18f,0.39f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,0.83f,0.16f),new Vector3(0.42f,0.19f,0.09f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.32f,1.32f,-0.13f),new Vector3(0.18f,0.16f,0.26f),new Vector3(0f,-13f,-12f)),
                        new BoxSpec(new Vector3(0.32f,1.32f,-0.13f),new Vector3(0.18f,0.16f,0.26f),new Vector3(0f,13f,12f)),
                    }),
                    new PartSpec("Stone brow",0,0,new Vector3(0f,1.68f,-0.015f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.69f,-0.035f),new Vector3(0.51f,0.37f,0.4f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.85f,0.1f),new Vector3(0.57f,0.075f,0.35f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.55f,0.205f),new Vector3(0.24f,0.13f,0.1f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Face shadow",0,1,new Vector3(0f,1.66f,0.224f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.66f,0.224f),new Vector3(0.36f,0.18f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left rune eye",0,2,new Vector3(-0.13f,1.68f,0.251f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.13f,1.68f,0.251f),new Vector3(0.1f,0.07f,0.025f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right rune eye",0,2,new Vector3(0.13f,1.68f,0.251f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.13f,1.68f,0.251f),new Vector3(0.1f,0.07f,0.025f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Chest rune",0,2,new Vector3(0f,1.12f,0.257f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.12f,0.257f),new Vector3(0.16f,0.24f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Hip core",0,1,new Vector3(0f,0.58f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.58f,0f),new Vector3(0.43f,0.2f,0.34f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left leg",0,0,new Vector3(-0.23f,0.29f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.23f,0.44f,0f),new Vector3(0.27f,0.24f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.18f,-0.015f),new Vector3(0.2484f,0.23f,0.29f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.045f,0.07f),new Vector3(0.27f,0.09f,0.43f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.39f,0.17f),new Vector3(0.18f,0.1f,0.07f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.068f,0.245f),new Vector3(0.23f,0.06f,0.07f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left stone arm",1,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.15f,0.015f),new Vector3(0.255f,0.3f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.435f,0.025f),new Vector3(0.2346f,0.22f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.06f),new Vector3(0.255f,0.14f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.68f,0.15f),new Vector3(0.12f,0.1f,0.16f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right leg",0,0,new Vector3(0.23f,0.29f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.23f,0.44f,0f),new Vector3(0.27f,0.24f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.18f,-0.015f),new Vector3(0.2484f,0.23f,0.29f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.045f,0.07f),new Vector3(0.27f,0.09f,0.43f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.39f,0.17f),new Vector3(0.18f,0.1f,0.07f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.068f,0.245f),new Vector3(0.23f,0.06f,0.07f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right stone arm",2,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.15f,0.015f),new Vector3(0.255f,0.3f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.435f,0.025f),new Vector3(0.2346f,0.22f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.06f),new Vector3(0.255f,0.14f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.68f,0.15f),new Vector3(0.12f,0.1f,0.16f),new Vector3(0f,0f,0f)),
                    }),
                };
                case Style.Shade: return new PartSpec[] {
                    new PartSpec("Torso",0,0,new Vector3(0f,1.04f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.06f,-0.105f),new Vector3(0.15f,0.65f,0.21f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,0.75f,-0.07f),new Vector3(0.44f,0.14f,0.32f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.2f,0.87f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,-8f,-7f)),
                        new BoxSpec(new Vector3(0.2f,0.87f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,8f,7f)),
                        new BoxSpec(new Vector3(-0.2f,1f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,-8f,-5f)),
                        new BoxSpec(new Vector3(0.2f,1f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,8f,5f)),
                        new BoxSpec(new Vector3(-0.2f,1.13f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,-8f,-3f)),
                        new BoxSpec(new Vector3(0.2f,1.13f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,8f,3f)),
                        new BoxSpec(new Vector3(-0.2f,1.26f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,-8f,-1f)),
                        new BoxSpec(new Vector3(0.2f,1.26f,0.045f),new Vector3(0.29f,0.065f,0.32f),new Vector3(0f,8f,1f)),
                    }),
                    new PartSpec("Stone brow",0,0,new Vector3(0f,1.68f,-0.015f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.86f,-0.035f),new Vector3(0.64f,0.13f,0.48f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.265f,1.67f,-0.015f),new Vector3(0.11f,0.34f,0.45f),new Vector3(0f,0f,-5f)),
                        new BoxSpec(new Vector3(0.265f,1.67f,-0.015f),new Vector3(0.11f,0.34f,0.45f),new Vector3(0f,0f,5f)),
                        new BoxSpec(new Vector3(0f,1.58f,-0.21f),new Vector3(0.47f,0.28f,0.08f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Face shadow",0,1,new Vector3(0f,1.69f,0.18f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.69f,0.18f),new Vector3(0.38f,0.18f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left rune eye",0,2,new Vector3(-0.13f,1.71f,0.21f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.13f,1.71f,0.21f),new Vector3(0.1f,0.07f,0.025f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right rune eye",0,2,new Vector3(0.13f,1.71f,0.21f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.13f,1.71f,0.21f),new Vector3(0.1f,0.07f,0.025f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Shade crown",0,2,new Vector3(0f,1.98f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.98f,0f),new Vector3(0.25f,0.2f,0.25f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,2.085f,0f),new Vector3(0.1f,0.07f,0.1f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.2f,1.96f,-0.015f),new Vector3(0.07f,0.17f,0.07f),new Vector3(0f,0f,-12f)),
                        new BoxSpec(new Vector3(0.2f,1.96f,-0.015f),new Vector3(0.07f,0.17f,0.07f),new Vector3(0f,0f,12f)),
                    }),
                    new PartSpec("Shade focus",2,2,new Vector3(0f,-0.66f,0.19f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.66f,0.19f),new Vector3(0.22f,0.22f,0.22f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.66f,0.19f),new Vector3(0.085f,0.25f,0.085f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Chest rune",0,2,new Vector3(0f,1.12f,0.228f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.12f,0.228f),new Vector3(0.16f,0.24f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Hip core",0,1,new Vector3(0f,0.58f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.58f,0f),new Vector3(0.37f,0.2f,0.34f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left leg",0,0,new Vector3(-0.23f,0.29f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.23f,0.44f,0f),new Vector3(0.19f,0.24f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.18f,-0.015f),new Vector3(0.1748f,0.23f,0.29f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.23f,0.045f,0.07f),new Vector3(0.19f,0.09f,0.43f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.29f,0.04f,0.26f),new Vector3(0.055f,0.08f,0.13f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.17f,0.04f,0.26f),new Vector3(0.055f,0.08f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left stone arm",1,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.15f,0.015f),new Vector3(0.2f,0.3f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.435f,0.025f),new Vector3(0.184f,0.22f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.06f),new Vector3(0.2f,0.14f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.07f,-0.7f,0.18f),new Vector3(0.045f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.7f,0.18f),new Vector3(0.045f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.07f,-0.7f,0.18f),new Vector3(0.045f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right leg",0,0,new Vector3(0.23f,0.29f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.23f,0.44f,0f),new Vector3(0.19f,0.24f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.18f,-0.015f),new Vector3(0.1748f,0.23f,0.29f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.23f,0.045f,0.07f),new Vector3(0.19f,0.09f,0.43f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.17f,0.04f,0.26f),new Vector3(0.055f,0.08f,0.13f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.29f,0.04f,0.26f),new Vector3(0.055f,0.08f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right stone arm",2,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,-0.15f,0.015f),new Vector3(0.2f,0.3f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.435f,0.025f),new Vector3(0.184f,0.22f,0.31f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.615f,0.06f),new Vector3(0.2f,0.14f,0.34f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.07f,-0.7f,0.18f),new Vector3(0.045f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,-0.7f,0.18f),new Vector3(0.045f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.07f,-0.7f,0.18f),new Vector3(0.045f,0.16f,0.075f),new Vector3(0f,0f,0f)),
                    }),
                };
                case Style.Spider: return new PartSpec[] {
                    new PartSpec("Torso",0,0,new Vector3(0f,1.04f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.94f,-0.25f),new Vector3(0.71f,0.46f,0.69f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.105f,-0.29f),new Vector3(0.54f,0.15f,0.54f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,0.88f,0.24f),new Vector3(0.53f,0.35f,0.47f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,1.19f,-0.3f),new Vector3(0.3f,0.055f,0.36f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Stone brow",0,0,new Vector3(0f,1.68f,-0.015f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.76f,0.36f),new Vector3(0.55f,0.29f,0.4f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,0.86f,0.41f),new Vector3(0.61f,0.1f,0.3f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0f,0.66f,0.5f),new Vector3(0.4f,0.1f,0.2f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Face shadow",0,1,new Vector3(0f,0.77f,0.575f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.77f,0.575f),new Vector3(0.43f,0.18f,0.035f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left rune eye",0,2,new Vector3(-0.13f,0.79f,0.602f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.075f,0.79f,0.602f),new Vector3(0.045f,0.045f,0.026f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.14f,0.825f,0.602f),new Vector3(0.045f,0.045f,0.026f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(-0.205f,0.79f,0.602f),new Vector3(0.045f,0.045f,0.026f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right rune eye",0,2,new Vector3(0.13f,0.79f,0.602f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.075f,0.79f,0.602f),new Vector3(0.045f,0.045f,0.026f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.14f,0.825f,0.602f),new Vector3(0.045f,0.045f,0.026f),new Vector3(0f,0f,0f)),
                        new BoxSpec(new Vector3(0.205f,0.79f,0.602f),new Vector3(0.045f,0.045f,0.026f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Chest rune",0,2,new Vector3(0f,1.12f,0.257f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,1.035f,0.516f),new Vector3(0.14f,0.2f,0.03f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Hip core",0,1,new Vector3(0f,0.58f,0f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0f,0.69f,-0.05f),new Vector3(0.56f,0.22f,0.67f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Left stone arm",1,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.3f,-0.65f,0.46f),new Vector3(0.07f,0.18f,0.1f),new Vector3(0f,0f,12f)),
                        new BoxSpec(new Vector3(0.3f,-0.7f,0.48f),new Vector3(0.08f,0.08f,0.14f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Right stone arm",2,0,new Vector3(0f,-0.33f,0.02f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.3f,-0.65f,0.46f),new Vector3(0.07f,0.18f,0.1f),new Vector3(0f,0f,-12f)),
                        new BoxSpec(new Vector3(-0.3f,-0.7f,0.48f),new Vector3(0.08f,0.08f,0.14f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 0",0,0,new Vector3(-0.29f,0.75f,-0.3168f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.37f,0.65f,-0.3144f),new Vector3(0.105f,0.105f,0.2711699f),new Vector3(51.3276197f,-88.281642f,0f)),
                        new BoxSpec(new Vector3(-0.475f,0.295f,-0.312f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,-90f,0f)),
                        new BoxSpec(new Vector3(-0.5f,0.025f,-0.287f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 1",0,0,new Vector3(0.29f,0.75f,-0.3168f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.37f,0.65f,-0.3144f),new Vector3(0.105f,0.105f,0.2711699f),new Vector3(51.3276197f,88.281642f,0f)),
                        new BoxSpec(new Vector3(0.475f,0.295f,-0.312f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,90f,0f)),
                        new BoxSpec(new Vector3(0.5f,0.025f,-0.287f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 2",0,0,new Vector3(-0.29f,0.75f,-0.1056f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.37f,0.65f,-0.1048f),new Vector3(0.105f,0.105f,0.27113f),new Vector3(51.3387944f,-89.4270613f,0f)),
                        new BoxSpec(new Vector3(-0.475f,0.295f,-0.104f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,-90f,0f)),
                        new BoxSpec(new Vector3(-0.5f,0.025f,-0.079f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 3",0,0,new Vector3(0.29f,0.75f,-0.1056f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.37f,0.65f,-0.1048f),new Vector3(0.105f,0.105f,0.27113f),new Vector3(51.3387944f,89.4270613f,0f)),
                        new BoxSpec(new Vector3(0.475f,0.295f,-0.104f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,90f,0f)),
                        new BoxSpec(new Vector3(0.5f,0.025f,-0.079f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 4",0,0,new Vector3(-0.29f,0.75f,0.1056f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.37f,0.65f,0.1048f),new Vector3(0.105f,0.105f,0.27113f),new Vector3(51.3387944f,-90.5729387f,0f)),
                        new BoxSpec(new Vector3(-0.475f,0.295f,0.104f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,-90f,0f)),
                        new BoxSpec(new Vector3(-0.5f,0.025f,0.129f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 5",0,0,new Vector3(0.29f,0.75f,0.1056f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.37f,0.65f,0.1048f),new Vector3(0.105f,0.105f,0.27113f),new Vector3(51.3387944f,90.5729387f,0f)),
                        new BoxSpec(new Vector3(0.475f,0.295f,0.104f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,90f,0f)),
                        new BoxSpec(new Vector3(0.5f,0.025f,0.129f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 6",0,0,new Vector3(-0.29f,0.75f,0.3168f),new BoxSpec[] {
                        new BoxSpec(new Vector3(-0.37f,0.65f,0.3144f),new Vector3(0.105f,0.105f,0.2711699f),new Vector3(51.3276197f,-91.718358f,0f)),
                        new BoxSpec(new Vector3(-0.475f,0.295f,0.312f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,-90f,0f)),
                        new BoxSpec(new Vector3(-0.5f,0.025f,0.337f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                    new PartSpec("Spider leg 7",0,0,new Vector3(0.29f,0.75f,0.3168f),new BoxSpec[] {
                        new BoxSpec(new Vector3(0.37f,0.65f,0.3144f),new Vector3(0.105f,0.105f,0.2711699f),new Vector3(51.3276197f,91.718358f,0f)),
                        new BoxSpec(new Vector3(0.475f,0.295f,0.312f),new Vector3(0.085f,0.085f,0.5274451f),new Vector3(84.4006607f,90f,0f)),
                        new BoxSpec(new Vector3(0.5f,0.025f,0.337f),new Vector3(0.11f,0.05f,0.13f),new Vector3(0f,0f,0f)),
                    }),
                };
                default:throw new ArgumentOutOfRangeException(nameof(style));
            }
        }
    }
}
