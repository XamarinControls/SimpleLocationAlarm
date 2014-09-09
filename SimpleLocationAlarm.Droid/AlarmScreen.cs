using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Gms.Location;
using Android.Util;
using Android.Gms.Maps;
using Android.Support.V7.App;
using Android.Support.V4.App;
using Android.Gms.Maps.Model;

namespace SimpleLocationAlarm.Droid
{
    [Activity(
        ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    public class AlarmScreen : ActionBarActivity
    {
        const string TAG = "AlarmScreen";

        bool _success;
        AlarmData _alarm;

        DBManager _dbManager = new DBManager();

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            if (LocationClient.HasError(Intent))
            {
                Log.Debug(TAG, "OnCreate : LocationClient HasError");
                _dbManager.DisableAll();                   

                SetContentView(Resource.Layout.AlarmError);                
            }
            else
            {
                Log.Debug(TAG, "OnCreate trigered by geofences");
                
                _success = true;                

                var geofences = LocationClient.GetTriggeringGeofences(Intent);
                if (geofences != null && geofences.Count > 0) {
                    _alarm = _dbManager.GetAlarmByGeofenceRequestId(geofences[0].RequestId);                    
                }

                SetContentView(Resource.Layout.AlarmGeofenceTriggered);

                if (_alarm != null)
                {
                    SupportActionBar.Title = _alarm.Name;
                }
            }
        }

        GoogleMap _map;

        protected override void OnStart()
        {
            base.OnStart();

            if (_success)
            {
                _map = (SupportFragmentManager.FindFragmentById(Resource.Id.map) as SupportMapFragment).Map;

                if (_map != null) 
                {
                    _map.MyLocationEnabled = true;

                    if (_alarm != null)
                    {
                        var position = new LatLng(_alarm.Latitude, _alarm.Longitude);

                        _map.AddCircle(new CircleOptions()
                            .InvokeCenter(position)
                            .InvokeRadius(_alarm.Radius));

                        _map.AddMarker(new MarkerOptions()
                            .SetPosition(position)
                            .InvokeIcon(BitmapDescriptorFactory.FromResource(Resource.Drawable.alarm_marker_selected)));

                        _map.MoveCamera(CameraUpdateFactory.NewLatLngZoom(position, _map.MaxZoomLevel - 6));
                    }
                }
            }
        }

        protected override void OnStop()
        {
            if (_success)
            {
                if (_map != null) {
                    _map.Clear();
                }

                _map = null;
            }

            base.OnStop();
        }
    }
}