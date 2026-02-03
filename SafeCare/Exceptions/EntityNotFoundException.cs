namespace SafeCare.Exceptions
{
    public class EntityNotFoundException : Exception
    {
        public EntityNotFoundException(Type entity, int id)
            : base($"Nie odnaleziono rekordu {Utils.DisplayHelper.GetDisplayName(entity)} o ID {id}")
        {
        }

        public EntityNotFoundException(Type entity, int[] ids)
            : base($"Nie odnaleziono rekordów {Utils.DisplayHelper.GetDisplayName(entity)} o ID {string.Join(", ", ids)}")
        {
        }
    }
}
