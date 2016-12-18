﻿using PacketDotNet;
using System.Collections.Generic;
using System.Linq;
using XPloit.Core;
using XPloit.Core.Attributes;
using XPloit.Core.Enums;
using XPloit.Core.Interfaces;
using XPloit.Core.Requirements.Payloads;
using XPloit.Helpers;
using XPloit.Helpers.Attributes;
using XPloit.Sniffer;
using XPloit.Sniffer.Filters;
using XPloit.Sniffer.Interfaces;
using XPloit.Sniffer.Streams;

namespace Auxiliary.Local
{
    public class Sniffer : Module
    {
        public interface IPayloadSniffer
        {
            bool CaptureOnTcpStream { get; }
            bool CaptureOnPacket { get; }

            void OnTcpStream(TcpStream stream);
            bool Check();
            void OnPacket(IPProtocolType protocolType, IpPacket packet);
        }

        string _CaptureDevice;

        #region Configure
        public override string Author { get { return "Fernando Díaz Toledano"; } }
        public override string Description { get { return "Sniffer"; } }
        public override Reference[] References
        {
            get { return new Reference[] { new Reference(EReferenceType.INFO, "For outward, requiere open Firewall for promiscuous mode"), }; }
        }
        public override IPayloadRequirements PayloadRequirements
        {
            get { return new InterfacePayload(typeof(IPayloadSniffer)); }
        }
        #endregion

        #region Properties
        [ConfigurableProperty(Description = "Sniff this port", Optional = true)]
        public ushort[] FilterPorts { get; set; }
        [ConfigurableProperty(Description = "Filter protocols", Optional = true)]
        public IPProtocolType[] FilterProtocols { get; set; }
        [ConfigurableProperty(Description = "Filter only the Tor Request")]
        public bool FilterOnlyTorRequest { get; set; }
        [AutoFill("GetAllDevices")]
        [ConfigurableProperty(Description = "Capture device or pcap file")]
        public string CaptureDevice
        {
            get
            {
                if (string.IsNullOrEmpty(_CaptureDevice)) _CaptureDevice = NetworkSniffer.CaptureDevices.FirstOrDefault();
                return _CaptureDevice;
            }
            set { _CaptureDevice = value; }
        }
        #endregion

        public Sniffer()
        {
            FilterProtocols = new IPProtocolType[] { IPProtocolType.TCP, IPProtocolType.UDP };
        }
        public string[] GetAllDevices() { return NetworkSniffer.CaptureDevices; }
        [NonJobable()]
        public override bool Run()
        {
            IPayloadSniffer pay = (IPayloadSniffer)Payload;
            //if (!SystemHelper.IsAdministrator())
            //    WriteError("Require admin rights");
            if (!pay.Check()) return false;
            if (FilterOnlyTorRequest) TorHelper.UpdateTorExitNodeList(true);

            NetworkSniffer s = new NetworkSniffer(CaptureDevice);
            if (pay.CaptureOnTcpStream) s.OnTcpStream += pay.OnTcpStream;
            if (pay.CaptureOnPacket) s.OnPacket += pay.OnPacket;

            List<IIpPacketFilter> filters = new List<IIpPacketFilter>();

            if (FilterOnlyTorRequest) filters.Add(new SnifferTorFilter());
            if (FilterPorts != null && FilterPorts.Length > 0) filters.Add(new SnifferPortFilter(FilterPorts));
            if (FilterProtocols != null && FilterProtocols.Length > 0) filters.Add(new SnifferProtocolFilter(FilterProtocols));

            s.Filters = filters.ToArray();
            s.Start();

            CreateJob(s, "IsDisposed");
            return true;
        }
        public override ECheck Check()
        {
            NetworkSniffer s = null;
            try
            {
                //if (!SystemHelper.IsAdministrator())
                //    WriteError("Require admin rights");

                IPayloadSniffer pay = (IPayloadSniffer)Payload;
                if (!pay.Check()) return ECheck.Error;

                s = new NetworkSniffer(CaptureDevice);
                s.Start();

                return ECheck.Ok;
            }
            catch { return ECheck.Error; }
            finally
            {
                if (s != null)
                    s.Dispose();
            }
        }
    }
}