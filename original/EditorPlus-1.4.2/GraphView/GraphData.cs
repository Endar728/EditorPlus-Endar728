namespace EditorPlus
{
    public sealed partial class GraphView
    {
        public struct ObjectiveDTO
        {
            public string Id, UniqueName, DisplayName, TypeName;
            public bool Hidden;
            public int OutcomeCount;
            public int Layer;
            public int Row;
            public string FactionName;
        }

        public struct OutcomeDTO
        {
            public string Id, UniqueName, TypeName;
            public int UsedByCount;
            public int Layer;
            public int Row;
        }

        public struct LinkDTO
        {
            public string FromId;
            public bool FromIsObjective;
            public string ToId;
            public bool ToIsObjective;
        }

        public struct GraphData
        {
            public ObjectiveDTO[] Objectives;
            public OutcomeDTO[] Outcomes;
            public LinkDTO[] Links;
        }
    }
}