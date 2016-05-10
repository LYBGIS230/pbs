//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.IO;
using PBS.DataSource;

namespace PBS.Service
{
    //UriTemplate class in WCF REST Service: Part I
    //http://www.c-sharpcorner.com/UploadFile/dhananjaycoder/1431/

    [ServiceContract(Namespace="")]
    public interface IPBSServiceProvider
    {
        [OperationContract]
        [WebGet(UriTemplate = "/clientaccesspolicy.xml",BodyStyle=WebMessageBodyStyle.Bare)]
        Stream ClientAccessPolicyFile();

        [OperationContract]
        [WebGet(UriTemplate = "/crossdomain.xml", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream CrossDomainFile();

        [OperationContract(Name = "GenerateServerInfo")]
        [WebGet(UriTemplate = "/KGIS/rest/info?f={f}&callback={callback}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateArcGISServerInfo(string f, string callback);

        [OperationContract(Name = "GenerateArcGISServerEndpointInfo")]
        [WebGet(UriTemplate = "/KGIS/rest/services?f={f}&callback={callback}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateArcGISServerEndpointInfo(string f,string callback);

        [OperationContract(Name = "GenerateArcGISServerEndpointInfo1")]
        [WebGet(UriTemplate = "/KGIS/rest/services/?f={f}&callback={callback}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateArcGISServerEndpointInfo1(string f, string callback);

        /// <summary>
        /// arcgis javascript api will invoke this request
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="f"></param>
        /// <param name="callBack"></param>
        /// <returns></returns>
        [OperationContract(Name = "GenerateArcGISServiceInfo")]
        [WebGet(UriTemplate = "/KGIS/rest/services/{servicename}/MapServer?f={f}&callback={callback}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateArcGISServiceInfo(string serviceName, string f, string callBack);

        /// <summary>
        /// arcgis.com viewer will invoke this request
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="operation"></param>
        /// <param name="f"></param>
        /// <param name="callBack"></param>
        /// <returns></returns>
        [OperationContract(Name = "GenerateArcGISServiceInfoIsSupportOperation")]
        [WebGet(UriTemplate = "/KGIS/rest/services/{servicename}/MapServer/{operation}?f={f}&callback={callback}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateArcGISServiceInfo(string serviceName, string operation,string f, string callBack);

        [OperationContract]
        [WebGet(UriTemplate = "/KGIS/rest/services/{servicename}/MapServer/tile/{level}/{row}/{col}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateArcGISTile(string serviceName, string level, string row, string col);

        [OperationContract]
        [WebGet(UriTemplate = "/KGIS/rest/services/{servicename}/OSMServer/{level}/{col}/{row}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateOSMTile(string serviceName, string level, string row, string col);

        [OperationContract]
        [WebGet(UriTemplate = "/KGIS/rest/services/{servicename}/MapServer/tile/{level}/{row}/{col}?type={type}&t={time}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateBaiduTile(string serviceName, string level, string row, string col, string type, string time);

        [OperationContract]
        [WebGet(UriTemplate = "/Other/rest/services/{servicename}/MapServer/tile/{level}/{row}/{col}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GetPGISTile(string serviceName, string level, string row, string col);
        [OperationContract]
        [WebGet(UriTemplate = "/KGIS/rest/services/{servicename}/MapServer/Origin", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GetServiceOrigin(string serviceName);

        [OperationContract]
        [WebGet(UriTemplate = "/KGIS/rest/services/{servicename}/MapServer/version?type={type}&s={startTimeStamp}&e={endTimeStamp}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GetVersions(string serviceName, string type, string startTimeStamp, string endTimeStamp);

        #region WMTS
        /// <summary>
        /// WMTS Capabilities resource
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        [OperationContract(Name = "GenerateWMTSCapabilitiesRESTful")]
        [WebGet(UriTemplate = "/KGIS/rest/services/{servicename}/MapServer/WMTS/{version}/WMTSCapabilities.xml", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateWMTSCapabilitiesRESTful(string serviceName, string version);

        [OperationContract(Name = "GenerateWMTSCapabilitiesRedirect")]
        [WebGet(UriTemplate = "/KGIS/rest/services/{servicename}/MapServer/WMTS", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateWMTSCapabilitiesRedirect(string serviceName);

        [OperationContract(Name = "GenerateWMTSCapabilitiesKVP")]
        [WebGet(UriTemplate = "/KGIS/rest/services/{servicename}/MapServer/WMTS?service=WMTS&request=GetCapabilities&version={version}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateWMTSCapabilitiesKVP(string serviceName, string version);

        /// <summary>
        /// WMTS Tile resource
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="version"></param>
        /// <param name="style"></param>
        /// <param name="tilematrixset"></param>
        /// <param name="tilematrix"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        [OperationContract(Name = "GenerateWMTSTileRESTful")]
        [WebGet(UriTemplate = "/KGIS/rest/services/{servicename}/MapServer/WMTS/tile/{version}/{layer}/{style}/{tilematrixset}/{tilematrix}/{row}/{col}.{format}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateWMTSTileRESTful(string serviceName, string version, string layer, string style, string tilematrixset, string tilematrix, string row, string col, string format);

        [OperationContract(Name = "GenerateWMTSTileKVP")]
        [WebGet(UriTemplate = "/KGIS/rest/services/{servicename}/MapServer/WMTS?service=WMTS&request=GetTile&version={version}&layer={layer}&style={style}&tileMatrixSet={tilematrixset}&tileMatrix={tilematrix}&tileRow={row}&tileCol={col}&format={format}", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GenerateWMTSTileKVP(string serviceName, string version, string layer, string style, string tilematrixset, string tilematrix, string row, string col, string format);
        #endregion

        #region admin api
        //WCF实现REST服务:http://www.cnblogs.com/wuhong/archive/2011/01/13/1934492.html
        [OperationContract(Name = "AddPBSService")]
        [WebInvoke(UriTemplate = "/KGIS/rest/admin/addService", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream AddPBSService(Stream requestBody);
        //[WebInvoke(UriTemplate = "/PBS/rest/admin/createservice?name={name}&port={port}&datasourcetype={datasourcetype}&datasourcepath={datasourcepath}&allowmemorycache={allowmemorycache}&disableclientcache={disableclientcache}&displaynodatatile={displaynodatatile}&visualstyle={visualstyle}&tilingschemepath={tilingschemepath}", BodyStyle = WebMessageBodyStyle.Bare)]
        //Stream CreatePBSService(string name, int port, DataSourceType datasourcetype, string datasourcepath, bool allowmemorycache = true, bool disableclientcache = false, bool displaynodatatile = false, VisualStyle visualstyle = VisualStyle.None, string tilingschemepath = null);

        [OperationContract(Name = "DeletePBSService")]
        [WebInvoke(UriTemplate = "/KGIS/rest/admin/deleteService", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream DeletePBSService(Stream requestBody);

        [OperationContract(Name = "ClearMemcacheByService")]
        [WebInvoke(UriTemplate = "/KGIS/rest/admin/memCache/clearByService", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream ClearMemcacheByService(Stream requestBody);

        [OperationContract(Name = "EnableMemcache")]
        [WebInvoke(UriTemplate = "/KGIS/rest/admin/memCache/enable", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream EnableMemcache(Stream requestBody);

        [OperationContract(Name = "DisableMemcache")]
        [WebInvoke(UriTemplate = "/KGIS/rest/admin/memCache/disable", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream DisableMemcache(Stream requestBody);

        [OperationContract(Name = "ChangeParamsArcGISDynamicMapService")]
        [WebInvoke(UriTemplate = "/KGIS/rest/admin/ArcGISDynamicMapService/changeParams", BodyStyle = WebMessageBodyStyle.Bare)]
        Stream ChangeArcGISDynamicMapServiceParams(Stream requestBody);
        #endregion
    }
}
