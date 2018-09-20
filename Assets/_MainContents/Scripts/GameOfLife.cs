namespace MainContents
{
    using System.Runtime.InteropServices;
    using UnityEngine;

    using Unity.Entities;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    [System.Serializable]
    public struct Resolution
    {
        public int Width;
        public int Height;
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
        /// 解像度
        /// </summary>
        [SerializeField] Resolution _resolution = new Resolution { Width = 1920, Height = 1080 };

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

        /// <summary>
        /// マテリアル書き込みデータ
        /// </summary>
        NativeArray<MaterialData> _writeMaterialData;

        // Shader.PropertyToIDs
        int _widthId, _heightId, _buffId;

        #endregion // Private Members


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
            this._maxCellsNum = this._resolution.Width * this._resolution.Height;
            // マテリアル及びバッファの生成
            this._materialInstance = new Material(this._material);
            this._writeMaterialbuffs = new ComputeBuffer(this._maxCellsNum, Marshal.SizeOf(typeof(MaterialData)));
            // ECS & JobSystem側で書き込むためのバッファを確保
            this._writeMaterialData = new NativeArray<MaterialData>(this._maxCellsNum, Allocator.Persistent);


            // ------------------------------
            // ECS関連の初期化

            // メモ:
            // ・DefaultWorldは生成時の負荷が高い上に使わなくても生かしておく事で余計な副作用(GCなど)が出る可能性がある。
            // 　こちらは「UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP」を定義することで自動生成を止めることが可能。
            // ・PureECSで実装するならDefaultWorldを消しても特に問題はないが、HybridECSで実装される方は以下のForumの内容に注意。
            // 　https://forum.unity.com/threads/disabling-automaticworldbootstrap-but-keeping-hybrid-injection-hooks.529675/

            // GOL専用のWorldを作成し必要なComponentSystemを登録していく
            World.Active = new World("GOL World");
            World.Active.CreateManager(typeof(EntityManager));
            // ComponentSystemはCreateManager経由でコンストラクタを呼び出すことが可能。(CreateManager → Activator.CreateInstanceと呼び出されている)
            // その際に引数も渡すことが可能。
            if (this._isConwayGameOfLife)
            {
                World.Active.CreateManager(typeof(ConwayGOLSystem), this._writeMaterialData, this._resolution);
            }
            else
            {
                World.Active.CreateManager(typeof(WaveGOLSystem), this._writeMaterialData, this._resolution);
            }
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(World.Active);

            // セル(Entity)の生成
            var entityManager = World.Active.GetExistingManager<EntityManager>();
            var cellArcheyype = this._isConwayGameOfLife
                ? entityManager.CreateArchetype(ComponentType.Create<ConwayCellData>())
                : entityManager.CreateArchetype(ComponentType.Create<WaveCellData>());
            for (int i = 0; i < this._maxCellsNum; ++i)
            {
                var x = i % this._resolution.Width;
                var y = i / this._resolution.Width;
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
                    float nextState = (((float)x / (float)this._resolution.Width) + ((float)y / (float)this._resolution.Height) * rand);
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
            if (this._writeMaterialData.IsCreated) { this._writeMaterialData.Dispose(); }
            World.DisposeAllWorlds();
        }

        /// <summary>
        /// MonoBehaviour.OnRenderImage
        /// </summary>
        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (this._writeMaterialbuffs != null)
            {
                this._writeMaterialbuffs.SetData(this._writeMaterialData);
                this._materialInstance.SetInt(this._widthId, this._resolution.Width);
                this._materialInstance.SetInt(this._heightId, this._resolution.Height);
                this._materialInstance.SetBuffer(this._buffId, this._writeMaterialbuffs);
                Graphics.Blit(src, dest, this._materialInstance);
            }
        }

        #endregion // Unity Events
    }
}
