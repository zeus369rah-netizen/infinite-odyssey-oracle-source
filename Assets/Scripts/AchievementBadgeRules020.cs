namespace OdysseyOracle
{
    // Pure index/partition/layout rules. Progress remains owned by its existing save adapters.
    public static class AchievementBadgeRules020
    {
        public const int AtlasSide = 5;
        public const int AtlasCount = AtlasSide * AtlasSide;
        public const int OriginalCount = 20;
        public const int DisplayCount = 21;
        public const int MysteryCell = 20;
        public const int FlamesCell = 21;
        public const float CardWidth = 300;
        public const float CardHeight = 132;
        public const float Gap = 16;

        public struct Cell
        {
            public int X, Y, Width, Height;
        }

        public struct Layout
        {
            public int Columns, Rows;
            public float Width, Height;
        }

        public static Cell AtlasCell(int index, int width, int height)
        {
            if ((uint)index >= AtlasCount || width < AtlasSide || height < AtlasSide) return default;
            int column = index % AtlasSide;
            int row = index / AtlasSide;
            int left = (int)((long)column * width / AtlasSide);
            int right = (int)((long)(column + 1) * width / AtlasSide);
            int top = (int)((long)row * height / AtlasSide);
            int bottom = (int)((long)(row + 1) * height / AtlasSide);
            return new Cell { X = left, Y = height - bottom, Width = right - left, Height = bottom - top };
        }

        public static int DisplayCell(int row, bool earned, bool visible)
        {
            if ((uint)row >= DisplayCount || !(earned || visible)) return MysteryCell;
            return row < OriginalCount ? row : FlamesCell;
        }

        public static Layout Measure(float availableWidth)
        {
            if (float.IsNaN(availableWidth) || float.IsInfinity(availableWidth) || availableWidth <= 0)
                availableWidth = CardWidth;
            int columns = availableWidth >= CardWidth * 2 + Gap ? 2 : 1;
            int rows = (DisplayCount + columns - 1) / columns;
            return new Layout
            {
                Columns = columns,
                Rows = rows,
                Width = availableWidth < CardWidth ? availableWidth : CardWidth,
                Height = rows * CardHeight + (rows - 1) * Gap
            };
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Integer top-row-major atlas indices converted to Unity bottom-origin rectangles; Canvas reference pixels.
// Array Formats: Fixed 25 atlas cells and 21 presentation rows; two scalar value records, no heap storage or progress bits.
// Dependencies/Pointers: Existing discovery/flame save owners provide immutable earned/revealed inputs; no Unity Object or storage access.
