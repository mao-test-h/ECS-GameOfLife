namespace MainContents
{
    using System.Runtime.InteropServices;
    using UnityEngine;

    using Unity.Entities;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public class Resolution
    {
        public const int Width = 1920;
        public const int Height = 1080;
    }

    public struct MaterialData
    {
        public float State;
    }

    // Conway's Game of Life Data
    public struct ConwayCellData : IComponentData
    {
        public int Index;
        public byte State;
        public byte NextState;
        public int LiveCount;
    }

    // Wave Game of Life Data
    public struct WaveCellData : IComponentData
    {
        public int Index;
        public float State;
        public float NextState;
        public float LastState;
    }

    public sealed unsafe class GameOfLife : MonoBehaviour
    {
        // ------------------------------
        #region // Private Members(Editable)

        /// <summary>
        /// ライフゲーム用マテリアル
        /// </summary>
        [SerializeField] Material _material = null;

        /// <summary>
        /// TrueならConway's GOLを再生
        /// </summary>
        [SerializeField] bool _isConwayGameOfLife = false;

        #endregion // Private Members(Editable)

        // ------------------------------
        #region // Private Members

        /// <summary>
        /// 最大セル数
        /// </summary>
        int _maxCellsNum;

        /// <summary>
        /// マテリアルのインスタンスを保持
        /// </summary>
        Material _materialInstance;

        /// <summary>
        /// マテリアル書き込みデータ用のバッファ
        /// </summary>
        ComputeBuffer _writeMaterialbuffs;

        // Shader.PropertyToIDs
        int _widthId, _heightId, _buffId;

        #endregion // Private Members

        // ------------------------------
        #region // Private Members(Static)

        /// <summary>
        /// マテリアル書き込みデータ
        /// </summary>
        static NativeArray<MaterialData> WriteMaterialData;

        /// <summary>
        /// マテリアル書き込みデータのポインタ(Jobのデータ受け渡し用)
        /// </summary>
        public static void* WriteMaterialDataPrt;

        #endregion // Private Members(Static)


        // ----------------------------------------------------
        #region // Unity Events

        /// <summary>
        /// MonoBehaviour.Start
        /// </summary>
        void Start()
        {
            // MaterialのIDを拾っておく
            this._widthId = Shader.PropertyToID("_Width");
            this._heightId = Shader.PropertyToID("_Height");
            this._buffId = Shader.PropertyToID("_MaterialBuff");

            // 最大セル数
            this._maxCellsNum = Resolution.Width * Resolution.Height;
            // マテリアル及びバッファの生成
            this._materialInstance = new Material(this._material);
            this._writeMaterialbuffs = new ComputeBuffer(this._maxCellsNum, Marshal.SizeOf(typeof(MaterialData)));
            // バッファのポインタを確保(Job側に回すやつ)
            WriteMaterialData = new NativeArray<MaterialData>(this._maxCellsNum, Allocator.Persistent);
            WriteMaterialDataPrt = NativeArrayUnsafeUtility.GetUnsafePtr(WriteMaterialData);


            // ------------------------------
            // ECS関連の初期化

            // メモ:
            // DefaultWorldは生成時の負荷が高い上に使わなくても生かしておく事で余計な副作用(GCなど)が出る可能性がある。
            // こちらは「UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP」を定義することで自動生成を止めることが可能。

            // GOL専用のWorldを作成し必要なComponentSystemを登録していく
            World.Active = new World("GOL World");
            World.Active.CreateManager(typeof(EntityManager));
            if (this._isConwayGameOfLife)
            {
                World.Active.CreateManager(typeof(ConwayGOLSystem));
            }
            else
            {
                World.Active.CreateManager(typeof(WaveGOLSystem));
            }
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(World.Active);

            // セル(Entity)の生成
            var entityManager = World.Active.GetExistingManager<EntityManager>();
            var cellArcheyype = this._isConwayGameOfLife
                ? entityManager.CreateArchetype(ComponentType.Create<ConwayCellData>())
                : entityManager.CreateArchetype(ComponentType.Create<WaveCellData>());
            for (int i = 0; i < this._maxCellsNum; ++i)
            {
                var x = i % Resolution.Width;
                var y = i / Resolution.Width;
                var entity = entityManager.CreateEntity(cellArcheyype);

                if (this._isConwayGameOfLife)
                {
                    // Conway's Game of Life
                    byte ret = (byte)Random.Range(0, 2);
                    entityManager.SetComponentData(entity, new ConwayCellData
                    {
                        NextState = ret,
                        State = ret,
                        Index = i,
                    });
                }
                else
                {
                    // Wave Game of Life
                    int rand = Random.Range(0, 32);
                    float nextState = (((float)x / (float)Resolution.Width) + ((float)y / (float)Resolution.Height) * rand);
                    entityManager.SetComponentData(entity, new WaveCellData
                    {
                        NextState = nextState,
                        State = nextState,
                        LastState = 0f,
                        Index = i,
                    });
                }
            }
        }

        /// <summary>
        /// MonoBehaviour.OnDestroy
        /// </summary>
        void OnDestroy()
        {
            if (this._materialInstance != null) { Destroy(this._materialInstance); this._materialInstance = null; }
            if (this._writeMaterialbuffs != null) { this._writeMaterialbuffs.Release(); this._writeMaterialbuffs = null; }
            if (WriteMaterialData.IsCreated) { WriteMaterialData.Dispose(); }
            if (World.Active != null) { World.Active.Dispose(); }
        }

        /// <summary>
        /// MonoBehaviour.OnRenderImage
        /// </summary>
        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (this._writeMaterialbuffs != null)
            {
                this._writeMaterialbuffs.SetData(WriteMaterialData);
                this._materialInstance.SetInt(this._widthId, Resolution.Width);
                this._materialInstance.SetInt(this._heightId, Resolution.Height);
                this._materialInstance.SetBuffer(this._buffId, this._writeMaterialbuffs);
                Graphics.Blit(src, dest, this._materialInstance);
            }
        }

        #endregion // Unity Events
    }
}
