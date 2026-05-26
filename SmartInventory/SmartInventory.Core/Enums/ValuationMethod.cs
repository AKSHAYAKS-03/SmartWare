namespace SmartInventory.Core.Enums;

public enum ValuationMethod
{
    FIFO = 0, //Old stock is considered sold first.
    WeightedAverage = 1 //Uses average cost of all stock.
}
