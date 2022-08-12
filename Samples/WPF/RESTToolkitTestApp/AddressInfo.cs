using System;

public class AddressInfo
{
	//From Database
	public int PropertyID;
	public string Nickname;
	public string Address_Full; //put into query
	public int? Units;
	public int? RegionID;
	public string Notes;
	public string RecordSource;

	//From LocationRecog
	public string FormattedAddress { get; set; } //specific address of company
	public string EntityName { get; set; }
	public string URL { get; set; }
	public string Phone { get; set; }
	public string Type { get; set; }

	public string Lat { get; set; }
	public string Long { get; set; }
	public string AddressAtLocation { get; set; }

	public int CompanyID;
	public AddressInfo(int id, string nn, string add, int? u, int? rid, string notes, string rs)
	{
		PropertyID = id;
		Nickname = nn;
		Address_Full = add;
		Units = u;
		RegionID = rid;
		Notes = notes;
		RecordSource = rs;
	}

	public AddressInfo(string add, string name, string url, string phone, string type)
    {
		FormattedAddress = add;
		EntityName = name;
		URL = url;
		Phone = phone;
		Type = type;
    }

	public AddressInfo()
    {

    }
}
