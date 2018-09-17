namespace MainContents
{
    using Unity.Entities;
    using Unity.Burst;
    using Unity.Jobs;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe sealed class ConwayGOLSystem : JobComponentSystem
    {
        struct DataGroup
        {
            public readonly int Length;
            [ReadOnly] public ComponentDataArray<ConwayCellData> CellData;
        }
        [Inject] DataGroup _dataGroup;

        NativeArray<ConwayCellData> _cells;
        void* _cellsPtr = null;
        void* _writeDataPrt = null;

        protected override void OnCreateManager()
        {
            base.OnCreateManager();
        }

        protected override void OnDestroyManager()
        {
            if (this._cells.IsCreated) { this._cells.Dispose(); }
            this._cellsPtr = null;
            this._writeDataPrt = null;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (this._writeDataPrt == null)
            {
                this._cells = new NativeArray<ConwayCellData>(this._dataGroup.CellData.Length, Allocator.Persistent);
                this._cellsPtr = NativeArrayUnsafeUtility.GetUnsafePtr(this._cells);
                this._writeDataPrt = GameOfLife.WriteMaterialDataPrt;
            }
            this._dataGroup.CellData.CopyTo(this._cells, 0);
            var job = new CalcCellJob(this._cellsPtr, this._writeDataPrt);
            return job.Schedule(this, inputDeps);
        }

        [BurstCompile]
        unsafe struct CalcCellJob : IJobProcessComponentData<ConwayCellData>
        {
            int _width, _height;
            [NativeDisableUnsafePtrRestriction] void* _cellsPrt;
            [NativeDisableUnsafePtrRestriction] void* _writeDataPrt;

            public CalcCellJob(void* cellsPrt, void* writeDataPrt)
            {
                this._width = Resolution.Width;
                this._height = Resolution.Height;
                this._cellsPrt = cellsPrt;
                this._writeDataPrt = writeDataPrt;
            }

            public void Execute(ref ConwayCellData data)
            {
                int i = data.Index;

                int x = i % this._width;
                int y = i / this._width;

                // 自身のindexに対する8方のindexを取得
                int above = y - 1;
                int below = y + 1;
                int left = x - 1;
                int right = x + 1;

                if (above < 0) { above = this._height - 1; }
                if (below == this._height) { below = 0; }
                if (left < 0) { left = this._width - 1; }
                if (right == this._width) { right = 0; }

                int liveCount = 0;
                if (UnsafeUtility.ReadArrayElement<ConwayCellData>(this._cellsPrt, above * this._width + left).State == 1) { ++liveCount; }
                if (UnsafeUtility.ReadArrayElement<ConwayCellData>(this._cellsPrt, y * this._width + left).State == 1) { ++liveCount; }
                if (UnsafeUtility.ReadArrayElement<ConwayCellData>(this._cellsPrt, below * this._width + left).State == 1) { ++liveCount; }
                if (UnsafeUtility.ReadArrayElement<ConwayCellData>(this._cellsPrt, below * this._width + x).State == 1) { ++liveCount; }
                if (UnsafeUtility.ReadArrayElement<ConwayCellData>(this._cellsPrt, below * this._width + right).State == 1) { ++liveCount; }
                if (UnsafeUtility.ReadArrayElement<ConwayCellData>(this._cellsPrt, y * this._width + right).State == 1) { ++liveCount; }
                if (UnsafeUtility.ReadArrayElement<ConwayCellData>(this._cellsPrt, above * this._width + right).State == 1) { ++liveCount; }
                if (UnsafeUtility.ReadArrayElement<ConwayCellData>(this._cellsPrt, above * this._width + x).State == 1) { ++liveCount; }
                data.LiveCount = liveCount;

                if (data.State == 1)
                {
                    if ((data.LiveCount == 2) || (data.LiveCount == 3))
                    {
                        data.NextState = 1;
                    }
                    else
                    {
                        data.NextState = 0;
                    }
                }
                else
                {
                    if (data.LiveCount == 3)
                    {
                        data.NextState = 1;
                    }
                    else
                    {
                        data.NextState = 0;
                    }
                }
                data.State = data.NextState;

                // 結果を書き込む
                UnsafeUtility.WriteArrayElement(this._writeDataPrt, i, new MaterialData { State = (data.State == 1) ? 255f : 0f });
            }
        }
    }
}
