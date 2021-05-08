﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CgfConverter.CryEngineCore
{
    public abstract class Chunk : IBinaryChunk
    {
        protected static Random rnd = new Random();
        protected static List<int> alreadyPickedRandoms = new List<int>();

        private readonly static Dictionary<Type, Dictionary<uint, Func<dynamic>>> _chunkFactoryCache = new Dictionary<Type, Dictionary<uint, Func<dynamic>>> { };

        internal ChunkHeader _header;
        internal Model _model;

        public uint Offset { get; internal set; }
        public ChunkTypeEnum ChunkType { get; internal set; }
        internal uint Version;
        internal int ID;
        internal uint Size;
        public uint DataSize { get; set; }

        internal Dictionary<long, byte> SkippedBytes = new Dictionary<long, byte> { };

        public static Chunk New(ChunkTypeEnum chunkType, uint version)
        {
            switch (chunkType)
            {
                case ChunkTypeEnum.SourceInfo:
                    return Chunk.New<ChunkSourceInfo>(version);
                case ChunkTypeEnum.Timing:
                    return Chunk.New<ChunkTimingFormat>(version);
                case ChunkTypeEnum.ExportFlags:
                    return Chunk.New<ChunkExportFlags>(version);
                case ChunkTypeEnum.MtlName:
                    return Chunk.New<ChunkMtlName>(version);
                case ChunkTypeEnum.DataStream:
                    return Chunk.New<ChunkDataStream>(version);
                case ChunkTypeEnum.Mesh:
                    return Chunk.New<ChunkMesh>(version);
                case ChunkTypeEnum.MeshSubsets:
                    return Chunk.New<ChunkMeshSubsets>(version);
                case ChunkTypeEnum.Node:
                    return Chunk.New<ChunkNode>(version);
                case ChunkTypeEnum.Helper:
                    return Chunk.New<ChunkHelper>(version);
                case ChunkTypeEnum.Controller:
                    return Chunk.New<ChunkController>(version);
                case ChunkTypeEnum.SceneProps:
                    return Chunk.New<ChunkSceneProp>(version);
                case ChunkTypeEnum.MeshPhysicsData:
                    return Chunk.New<ChunkMeshPhysicsData>(version);
                case ChunkTypeEnum.BoneAnim:
                    return Chunk.New<ChunkBoneAnim>(version);
                // Compiled chunks
                case ChunkTypeEnum.CompiledBones:
                    return Chunk.New<ChunkCompiledBones>(version);
                case ChunkTypeEnum.CompiledPhysicalProxies:
                    return Chunk.New<ChunkCompiledPhysicalProxies>(version);
                case ChunkTypeEnum.CompiledPhysicalBones:
                    return Chunk.New<ChunkCompiledPhysicalBones>(version);
                case ChunkTypeEnum.CompiledIntSkinVertices:
                    return Chunk.New<ChunkCompiledIntSkinVertices>(version);
                case ChunkTypeEnum.CompiledMorphTargets:
                    return Chunk.New<ChunkCompiledMorphTargets>(version);
                case ChunkTypeEnum.CompiledExt2IntMap:
                    return Chunk.New<ChunkCompiledExtToIntMap>(version);
                case ChunkTypeEnum.CompiledIntFaces:
                    return Chunk.New<ChunkCompiledIntFaces>(version);
                // Star Citizen equivalents
                case ChunkTypeEnum.CompiledBonesSC:
                    return Chunk.New<ChunkCompiledBones>(version);
                case ChunkTypeEnum.CompiledPhysicalBonesSC:
                    return Chunk.New<ChunkCompiledPhysicalBones>(version);
                case ChunkTypeEnum.CompiledExt2IntMapSC:
                    return Chunk.New<ChunkCompiledExtToIntMap>(version);
                case ChunkTypeEnum.CompiledIntFacesSC:
                    return Chunk.New<ChunkCompiledIntFaces>(version);
                case ChunkTypeEnum.CompiledIntSkinVerticesSC:
                    return Chunk.New<ChunkCompiledIntSkinVertices>(version);
                case ChunkTypeEnum.CompiledMorphTargetsSC:
                    return Chunk.New<ChunkCompiledMorphTargets>(version);
                case ChunkTypeEnum.CompiledPhysicalProxiesSC:
                    return Chunk.New<ChunkCompiledPhysicalProxies>(version);
                // SC IVO chunks
                case ChunkTypeEnum.MtlNameIvo:
                    return Chunk.New<ChunkMtlName>(version);
                case ChunkTypeEnum.CompiledBonesIvo:
                    return Chunk.New<ChunkCompiledBones>(version);
                case ChunkTypeEnum.MeshIvo:
                    return Chunk.New<ChunkMesh>(version);
                // Old chunks
                case ChunkTypeEnum.BoneNameList:
                    return Chunk.New<ChunkBoneNameList>(version);
                case ChunkTypeEnum.MeshMorphTarget:
                    return Chunk.New<ChunkMeshMorphTargets>(version);
                case ChunkTypeEnum.BinaryXmlDataSC:
                    return Chunk.New<ChunkBinaryXmlData>(version);
                case ChunkTypeEnum.Mtl:
                    //Utils.Log(LogLevelEnum.Debug, "Mtl Chunk here");  // Obsolete.  Not used
                default:
                    return new ChunkUnknown();
            }
        }

        public static T New<T>(uint version) where T : Chunk
        {
            Dictionary<uint, Func<dynamic>> versionMap = null;
            Func<dynamic> factory = null;

            if (!_chunkFactoryCache.TryGetValue(typeof(T), out versionMap))
            {
                versionMap = new Dictionary<uint, Func<dynamic>> { };
                _chunkFactoryCache[typeof(T)] = versionMap;
            }

            if (!versionMap.TryGetValue(version, out factory))
            {
                var targetType = (from type in Assembly.GetExecutingAssembly().GetTypes()
                                  where !type.IsAbstract
                                  where type.IsClass
                                  where !type.IsGenericType
                                  where typeof(T).IsAssignableFrom(type)
                                  where type.Name == String.Format("{0}_{1:X}", typeof(T).Name, version)
                                  select type).FirstOrDefault();

                if (targetType != null)
                {
                    factory = () => { return Activator.CreateInstance(targetType) as T; };
                }

                _chunkFactoryCache[typeof(T)][version] = factory;
            }

            if (factory != null)
            {
                return factory.Invoke() as T;
            }

            throw new NotSupportedException(string.Format("Version {0:X} of {1} is not supported", version, typeof(T).Name));
        }

        public void Load(Model model, ChunkHeader header)
        {
            _model = model;
            _header = header;
        }

        public void SkipBytes(BinaryReader reader, long? bytesToSkip = null)
        {
            if (reader == null)
                return;

            if ((reader.BaseStream.Position > Offset + Size) && (Size > 0))
                Utils.Log(LogLevelEnum.Debug, "Buffer Overflow in {2} 0x{0:X} ({1} bytes)", ID, reader.BaseStream.Position - Offset - Size, GetType().Name);

            if (reader.BaseStream.Length < Offset + Size)
                Utils.Log(LogLevelEnum.Debug, "Corrupt Headers in {1} 0x{0:X}", ID, GetType().Name);

            if (!bytesToSkip.HasValue)
                bytesToSkip = (long)(Size - Math.Max(reader.BaseStream.Position - Offset, 0));

            for (long i = 0; i < bytesToSkip; i++)
            {
                SkippedBytes[reader.BaseStream.Position - Offset] = reader.ReadByte();
            }
        }

        public virtual void Read(BinaryReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            ChunkType = _header.ChunkType;
            Version = _header.Version;
            Offset = _header.Offset;
            ID = _header.ID;
            Size = _header.Size;
            DataSize = Size;          // For SC files, there is no header in chunks.  But need Datasize to calculate things.

            reader.BaseStream.Seek(_header.Offset, 0);

            // Star Citizen files don't have the type, version, offset and ID at the start of a chunk, so don't read them.
            if (_model.FileVersion == FileVersionEnum.CryTek_3_4 || _model.FileVersion == FileVersionEnum.CryTek_3_5)
            {
                ChunkType = (ChunkTypeEnum)Enum.ToObject(typeof(ChunkTypeEnum), reader.ReadUInt32());
                Version = reader.ReadUInt32();
                Offset = reader.ReadUInt32();
                ID = reader.ReadInt32();
                DataSize = Size - 16;
            }

            if (Offset != _header.Offset || Size != _header.Size)
            {
                Utils.Log(LogLevelEnum.Warning, "Conflict in chunk definition");
                Utils.Log(LogLevelEnum.Warning, "{0:X}+{1:X}", _header.Offset, _header.Size);
                Utils.Log(LogLevelEnum.Warning, "{0:X}+{1:X}", Offset, Size);
            }
        }

        /// <summary>
        /// Gets a link to the SkinningInfo model.
        /// </summary>
        /// <returns>Link to the SkinningInfo model.</returns>
        public SkinningInfo GetSkinningInfo()
        {
            if (_model.SkinningInfo == null)
            {
                _model.SkinningInfo = new SkinningInfo();
            }
            return _model.SkinningInfo;
        }

        public virtual void Write(BinaryWriter writer) { throw new NotImplementedException(); }

        public override string ToString()
        {
            return $@"Chunk Type: {ChunkType}, Ver: {Version:X}, Offset: {Offset:X}, ID: {ID:X}, Size: {Size}";
        }

        public static int GetNextRandom()
        {
            bool available = false;
            int rand = 0;
            while (!available) {
                rand = rnd.Next(100000);
                if (!alreadyPickedRandoms.Contains(rand))
                {
                    alreadyPickedRandoms.Add(rand);
                    available = true;
                }
            }
            return rand;
        }
    }
}
