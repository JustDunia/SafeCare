namespace SafeCare.Dtos
{
    public class DepartmentDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Code { get; set; }


        public override string ToString()
        {
            return Name;
        }
    }
}
