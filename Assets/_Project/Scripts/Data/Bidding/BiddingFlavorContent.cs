namespace ConstructionEmpire.Data.Bidding
{
    public static class BiddingFlavorContent
    {
        public static readonly string[] Clients = {
            "Mara and Ellis Fenwick", "Morrow Bay Logistics", "Juniper Table Restaurants", "Alder & Finch Offices", "Priya Bellamy",
            "The Rowan Household", "Cedar Lane Residents", "Amelia Northcroft", "Linden Works Studios", "Glassfern Retail Group",
            "Westmere Borough Council", "Brookhaven Public Library", "Northfield Community Schools", "Elmbridge Parks Authority", "Fairwater Housing Partnership"
        };
        public static readonly string[] Competitors = { "Leanline Construction", "Crestwell & Co.", "Paceforge Contractors", "Exactspan Projects", "Neighbourstone Builders" };
        public static readonly string[] Personalities = {
            "Wins on disciplined purchasing and lean overheads; rarely leads with premium finishes.",
            "Protects its reputation for exceptional finishes and charges accordingly.",
            "Commits crews early and prioritizes short, dependable delivery programmes.",
            "Pursues technically demanding fit-outs where focused expertise commands a premium.",
            "Builds repeat business through local relationships and practical site knowledge."
        };
        public static readonly string[] Won = {
            "Your bid for {project} has been accepted. Review the award before planning delivery.",
            "Contract awarded: {project}. Your proposal was selected by {client}.",
            "Successful tender for {project}. The agreed bid value is {amount}.",
            "{client} has selected your company for {project}. The opportunity is now marked Won."
        };
        public static readonly string[] Lost = {
            "Your bid for {project} was unsuccessful. The client selected another contractor.",
            "Tender result: {project} has been awarded elsewhere. Review your proposal for future opportunities.",
            "{client} did not select your proposal for {project}. This opportunity is now closed.",
            "No award for {project}. Your bid has been recorded as unsuccessful."
        };
        public static readonly string[] NarrowlyLost = {
            "Your proposal for {project} narrowly missed selection. Review price and delivery commitments.",
            "Close tender result: {project} was awarded to another contractor.",
            "{client} selected another proposal for {project} after a close result.",
            "{project}: your bid was competitive, but another contractor secured the award."
        };
    }
}
