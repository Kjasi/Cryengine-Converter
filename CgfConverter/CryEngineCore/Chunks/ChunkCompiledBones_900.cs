﻿using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CgfConverter.CryEngineCore
{
    public class ChunkCompiledBones_900 : ChunkCompiledBones
    {
        public override void Read(BinaryReader b)
        {
            base.Read(b);
            NumBones = b.ReadInt32();

            for (int i = 0; i < NumBones; i++)
            {
                CompiledBone tempBone = new CompiledBone();
                tempBone.ReadCompiledBone_900(b);

                if (RootBone == null)  // First bone read is root bone
                    RootBone = tempBone;

                BoneList.Add(tempBone);
            }

            List<string> boneNames = GetNullSeparatedStrings(NumBones, b);

            // Post bone read setup.  Parents, children, etc.
            // Add the ChildID to the parent bone.  This will help with navigation.
            for (int i = 0; i < NumBones; i++)
            {
                BoneList[i].boneName = boneNames[i];
                SetParentBone(BoneList[i]);
                AddChildIDToParent(BoneList[i]);
            }

            SkinningInfo skin = GetSkinningInfo();
            skin.CompiledBones = new List<CompiledBone>();
            skin.HasSkinningInfo = true;
            skin.CompiledBones = BoneList;
        }

        void SetParentBone(CompiledBone bone)
        {
            // offsetParent is really parent index.
            if (bone.offsetParent != -1)
            {
                bone.parentID = BoneList[bone.offsetParent].ControllerID;
                bone.ParentBone = BoneList[bone.offsetParent];
            }
        }

        protected List<string> GetNullSeparatedStrings(int numberOfNames, BinaryReader b)
        {
            List<string> names = new List<string>();
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < numberOfNames; i++)
            {    
                char c = b.ReadChar();
                while (c != 0)
                {
                    builder.Append(c);
                    c = b.ReadChar();
                }
                names.Add(builder.ToString());
                builder.Clear();
            }

            return names;
        }
    }
}
