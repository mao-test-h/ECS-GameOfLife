namespace MainContents
{
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Burst;
    using Unity.Jobs;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe sealed class WaveGOLSystem : JobComponentSystem
    {
        struct DataGroup
        {
            public readonly int Length;
            [ReadOnly] public ComponentDataArray<WaveCellData> CellData;
        }
        [Inject] DataGroup _dataGroup;

        NativeArray<WaveCellData> _cells;
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
                this._cells = new NativeArray<WaveCellData>(this._dataGroup.CellData.Length, Allocator.Persistent);
                this._cellsPtr = NativeArrayUnsafeUtility.GetUnsafePtr(this._cells);
                this._writeDataPrt = GameOfLife.WriteMaterialDataPrt;
            }
            this._dataGroup.CellData.CopyTo(this._cells, 0);
            var job = new CalcCellJob(this._cellsPtr, this._writeDataPrt);
            return job.Schedule(this, inputDeps);
        }

        [BurstCompile]
        unsafe struct CalcCellJob : IJobProcessComponentData<WaveCellData>
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

            public void Execute(ref WaveCellData data)
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

                float totalState = 0f;
                totalState += UnsafeUtility.ReadArrayElement<WaveCellData>(this._cellsPrt, above * this._width + left).State;
                totalState += UnsafeUtility.ReadArrayElement<WaveCellData>(this._cellsPrt, y * this._width + left).State;
                totalState += UnsafeUtility.ReadArrayElement<WaveCellData>(this._cellsPrt, below * this._width + left).State;
                totalState += UnsafeUtility.ReadArrayElement<WaveCellData>(this._cellsPrt, below * this._width + x).State;

                totalState += UnsafeUtility.ReadArrayElement<WaveCellData>(this._cellsPrt, below * this._width + right).State;
                totalState += UnsafeUtility.ReadArrayElement<WaveCellData>(this._cellsPrt, y * this._width + right).State;
                totalState += UnsafeUtility.ReadArrayElement<WaveCellData>(this._cellsPrt, above * this._width + right).State;
                totalState += UnsafeUtility.ReadArrayElement<WaveCellData>(this._cellsPrt, above * this._width + x).State;

                float average = math.floor(totalState / 8f);
                if (average >= 255)
                {
                    data.NextState = 0;
                }
                else if (average <= 0)
                {
                    data.NextState = 255;
                }
                else
                {
                    data.NextState = data.State + average;
                    if (data.LastState > 0) { data.NextState -= data.LastState; }
                    if (data.NextState > 255) { data.NextState = 255; }
                    else if (data.NextState < 0) { data.NextState = 0; }
                }
                data.LastState = data.State;
                data.State = data.NextState;

                // 結果を書き込む
                UnsafeUtility.WriteArrayElement(this._writeDataPrt, i, new MaterialData { State = data.State / 255f });
            }
        }
    }
}
