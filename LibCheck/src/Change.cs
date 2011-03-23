﻿//------------------------------------------------------------------------------
// <autogenerated>
//     This code was generated by a tool.
//     Runtime Version: 1.0.2523.0
//
//     Changes to this file may cause incorrect behavior and will be lost if 
//     the code is regenerated.
// </autogenerated>
//------------------------------------------------------------------------------

using System.Xml.Serialization;
using System.Web.Services.Protocols;
using System.Web.Services;


[System.Web.Services.WebServiceBindingAttribute(Name="ChangeSoap", Namespace="http://tempuri.org/")]
public class Change : System.Web.Services.Protocols.SoapHttpClientProtocol {
    
    public Change() {
        this.Url = "http://brada55/change.asmx";
    }
    
    [System.Web.Services.Protocols.SoapMethodAttribute("http://tempuri.org/GetChanges", MessageStyle=System.Web.Services.Protocols.SoapMessageStyle.ParametersInDocument)]
    public System.Data.DataSet GetChanges(string typeName) {
        object[] results = this.Invoke("GetChanges", new object[] {typeName});
        return ((System.Data.DataSet)(results[0]));
    }
    
    public System.IAsyncResult BeginGetChanges(string typeName, System.AsyncCallback callback, object asyncState) {
        return this.BeginInvoke("GetChanges", new object[] {typeName}, callback, asyncState);
    }
    
    public System.Data.DataSet EndGetChanges(System.IAsyncResult asyncResult) {
        object[] results = this.EndInvoke(asyncResult);
        return ((System.Data.DataSet)(results[0]));
    }
    
    [System.Web.Services.Protocols.SoapMethodAttribute("http://tempuri.org/GetChangesByBluild", MessageStyle=System.Web.Services.Protocols.SoapMessageStyle.ParametersInDocument)]
    public System.Data.DataSet GetChangesByBluild(string typeName, string bldNum) {
        object[] results = this.Invoke("GetChangesByBluild", new object[] {typeName,
                    bldNum});
        return ((System.Data.DataSet)(results[0]));
    }
    
    public System.IAsyncResult BeginGetChangesByBluild(string typeName, string bldNum, System.AsyncCallback callback, object asyncState) {
        return this.BeginInvoke("GetChangesByBluild", new object[] {typeName,
                    bldNum}, callback, asyncState);
    }
    
    public System.Data.DataSet EndGetChangesByBluild(System.IAsyncResult asyncResult) {
        object[] results = this.EndInvoke(asyncResult);
        return ((System.Data.DataSet)(results[0]));
    }
}