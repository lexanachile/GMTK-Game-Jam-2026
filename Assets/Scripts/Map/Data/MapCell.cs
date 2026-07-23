public enum MapCellType
{
    Unknown,
    Empty,
    Rock,
    Water,
    Road,
    Forest,
    Building
}

public struct MapCell
{
    public bool explored;
    public MapCellType type;
}