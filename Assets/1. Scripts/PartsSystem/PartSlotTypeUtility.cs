public static class PartSlotTypeUtility
{
    public static PartSlotType Normalize(PartSlotType slot)
    {
        return slot == PartSlotType.Utility ? PartSlotType.Support : slot;
    }

    public static bool Matches(PartSlotType partSlot, PartSlotType targetSlot)
    {
        return Normalize(partSlot) == Normalize(targetSlot);
    }

    public static string GetKoreanLabel(PartSlotType slot)
    {
        switch (Normalize(slot))
        {
            case PartSlotType.Head:
                return "머리";
            case PartSlotType.Arm:
                return "팔";
            case PartSlotType.Leg:
                return "다리";
            case PartSlotType.Support:
                return "보조";
            case PartSlotType.Core:
                return "코어";
            default:
                return "보조";
        }
    }
}
