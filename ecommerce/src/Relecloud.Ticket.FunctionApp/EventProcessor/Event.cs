using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Relecloud.Ticket.FunctionApp.EventProcessor
{
    public class Event
    {
        public string EventType { get; set; }
        public string EntityId { get; set; }
    }
}
