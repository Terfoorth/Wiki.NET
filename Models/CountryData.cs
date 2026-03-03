namespace Wiki_Blaze
{
    public class CountryData
    {
        public CountryData(int regionId, int parentRegionId, string region, double area)
        {
            RegionID = regionId;
            ParentRegionID = parentRegionId;
            Region = region;
            Area = area;
        }
        public int RegionID { get; set; }
        public int ParentRegionID { get; set; }
        public string Region { get; set; }
        public double Area { get; set; }

        public IEnumerable<CountryData> GetCountryData()
        => [
                new CountryData(1, 0, "Norway", 385207),
                new CountryData(2, 0, "Sweden", 528447),
                new CountryData(3, 0, "Denmark", 42951),
                new CountryData(4, 0, "Finland", 338455),
                new CountryData(5, 0, "Iceland", 103000),
                new CountryData(6, 0, "Ireland", 84421),
                new CountryData(7, 0, "United Kingdom", 243610),
                new CountryData(18, 17, "Spain", 505990),
                new CountryData(19, 17, "Portugal", 92212),
                new CountryData(20, 17, "Greece", 131957),
                new CountryData(21, 17, "Italy", 301230),
                new CountryData(22, 17, "Malta", 316),
                new CountryData(23, 17, "San Marino", 61.2),
                new CountryData(25, 17, "Serbia", 88499),
                new CountryData(27, 26, "USA", 9522055),
                new CountryData(28, 26, "Canada", 9984670),
                new CountryData(30, 29, "Argentina", 2780400),
                new CountryData(31, 29, "Brazil", 8514215),
                new CountryData(34, 32, "India", 3287263),
                new CountryData(35, 32, "Japan", 377975)];
    }
}