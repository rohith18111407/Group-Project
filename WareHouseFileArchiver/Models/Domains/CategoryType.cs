using System.Runtime.Serialization;

namespace WareHouseFileArchiver.Models.Domains
{
    public enum CategoryType
    {
        [EnumMember(Value = "Uncategorized")]
        Uncategorized,

        [EnumMember(Value = "Policies")]
        Policies,

        [EnumMember(Value = "Invoice")]
        Invoice,

        [EnumMember(Value = "Reports")]
        Reports,

        [EnumMember(Value = "Blueprint")]
        Blueprint,

        [EnumMember(Value = "SafetyInstructions")]
        SafetyInstructions,

        [EnumMember(Value = "Notices")]
        Notices,

        [EnumMember(Value = "Manuals")]
        Manuals,

        [EnumMember(Value = "MeetingMinutes")]
        MeetingMinutes,

        [EnumMember(Value = "Prescriptions")]
        Prescriptions,

        [EnumMember(Value = "Documentation")]
        Documentation,

        [EnumMember(Value = "ReleaseNotes")]
        ReleaseNotes,

        [EnumMember(Value = "TestCases")]
        TestCases,

        [EnumMember(Value = "DesignDocs")]
        DesignDocs,

        [EnumMember(Value = "Itinerary")]
        Itinerary,

        [EnumMember(Value = "FlightTickets")]
        FlightTickets,
        
        [EnumMember(Value = "HotelBookings")]
        HotelBookings,
    }

}