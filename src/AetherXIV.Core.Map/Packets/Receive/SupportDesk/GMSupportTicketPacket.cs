using AetherXIV.Core.Common;
using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.receive.supportdesk
{
    class GMSupportTicketPacket
    {
        public bool invalidPacket = false;
        public string ticketTitle, ticketBody;
        public uint ticketIssueIndex;
        public uint langCode;

        public GMSupportTicketPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try
                    {
                        langCode = binReader.ReadUInt32();
                        ticketIssueIndex = binReader.ReadUInt32();
                        ticketTitle = Utils.ReadNullTermString(binReader, 0x80);
                        ticketBody = Utils.ReadNullTermString(binReader, 0x800);
                    }
                    catch (Exception){
                        invalidPacket = true;
                    }
                }
            }
        }

    }
}
