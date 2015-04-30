namespace ActiveStruts.Util
{
    public class Tuple<T1, T2>
    {
        public Tuple(T1 item1, T2 item2)
        {
            this.Item1 = item1;
            this.Item2 = item2;
        }

        public Tuple()
        {
        }

        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }
    }

    public class Tuple<T1, T2, T3> : Tuple<T1, T2>
    {
        public Tuple(T1 item1, T2 item2, T3 item3) : base(item1, item2)
        {
            this.Item3 = item3;
        }

        public T3 Item3 { get; set; }
    }

    public class Tuple<T1, T2, T3, T4> : Tuple<T1, T2, T3>
    {
        public Tuple(T1 item1, T2 item2, T3 item3, T4 item4)
            : base(item1, item2, item3)
        {
            this.Item4 = item4;
        }

        public T4 Item4 { get; set; }
    }
}