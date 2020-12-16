namespace DustyBot.LastFm.Models
{
    public class LastFmScore<T>
    {
        public T Entity { get; }
        public double Score { get; }

        internal LastFmScore(T entity, double score)
        {
            Entity = entity;
            Score = score;
        }
    }
}
