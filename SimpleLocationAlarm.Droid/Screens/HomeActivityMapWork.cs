﻿using System.Linq;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using System.Collections.Generic;
using Android.Locations;
using Android.Util;
using SimpleLocationAlarm.Droid.Services;

namespace SimpleLocationAlarm.Droid.Screens
{
	public partial class HomeActivity : GoogleMap.IOnMapLoadedCallback
	{
		GoogleMap _map;

		BitmapDescriptor _alarm_marker_normal, _alarm_marker_selected, _alarm_marker_disabled;

        Circle _circleToAdd;

	    Marker _alarmToAdd;
        Marker AlarmToAddMarker
        {
            get
            {
                return _alarmToAdd;
            }
            set
            {
                if (_alarmToAdd != null)
                {
                    _alarmToAdd.Remove();
                }
                
                _alarmToAdd = value;

                RedrawAddCircle();
            }
        }

        List<AlarmData> _mapData = new List<AlarmData> ();
		List<Marker> _currentMarkers = new List<Marker> ();
		List<Circle> _currentCircles = new List<Circle> ();

		void AnimateTo (Location location)
		{
			if (location != null) {
				_map.AnimateCamera (CameraUpdateFactory.NewLatLngZoom (
					new LatLng (location.Latitude, location.Longitude), _map.MaxZoomLevel - 6));
			}
		}

		void FindMap ()
		{
			_map = (SupportFragmentManager.FindFragmentById (Resource.Id.map) as SupportMapFragment).Map;
			if (_map != null) {
				_map.MyLocationEnabled = true;

				_map.UiSettings.TiltGesturesEnabled = false;
				_map.UiSettings.RotateGesturesEnabled = false;

				_map.MapClick += OnMapClick;
				_map.MyLocationChange += HandleMyLocationChange;
				_map.MarkerClick += OnMarkerClick;

				// here because map should be already initialized
				// http://developer.android.com/reference/com/google/android/gms/maps/model/BitmapDescriptorFactory.html
				_alarm_marker_normal = BitmapDescriptorFactory.FromResource (Resource.Drawable.marker_violet);
				_alarm_marker_selected = BitmapDescriptorFactory.FromResource (Resource.Drawable.alarm_red);
                _alarm_marker_disabled = BitmapDescriptorFactory.FromResource(Resource.Drawable.marker_grey);
                
				RefreshData ();

				_map.SetOnMapLoadedCallback (this);

				if (Mode == Mode.Add) {
					if (AlarmToAddMarker != null) {
						AlarmToAddMarker = _map.AddMarker (new MarkerOptions ().SetPosition (AlarmToAddMarker.Position).InvokeIcon (_alarm_marker_normal));
					}
				}
			}
		}

		void RefreshData ()
		{
			_dbManager.InvokeDataUpdate ();
		}

		Location GetLastKnownLocation ()
		{
			var locationManager = LocationManager.FromContext (this);

			return locationManager.GetLastKnownLocation (LocationManager.GpsProvider) ??
			locationManager.GetLastKnownLocation (LocationManager.NetworkProvider);
		}

		Location _myCurrentLocation;

		Location MyCurrentLocation {
			get {
				return _myCurrentLocation ?? GetLastKnownLocation ();
			}
		}

		bool _wasZoomedToCurrentLocation;

		void HandleMyLocationChange (object sender, GoogleMap.MyLocationChangeEventArgs e)
		{
			Log.Debug (TAG, "New location detected");

			_myCurrentLocation = e.Location;

			if (!_wasZoomedToCurrentLocation) {
				ZoomToMyLocationAndAlarms ();
			}

			_wasZoomedToCurrentLocation = true;
		}

		void LooseMap ()
		{
			if (_map != null) {
				_map.MapClick -= OnMapClick;
				_map.MyLocationChange -= HandleMyLocationChange;
				_map.MarkerClick -= OnMarkerClick;

				_map.SetOnMapLoadedCallback (null);

				ClearMap ();

				_map = null;
			}
		}

		protected override void OnDataUpdated (object sender, AlarmsEventArgs e)
		{
			Log.Debug (TAG, "OnDataUpdated, count = " + e.Data.Count);

			_mapData = e.Data;

			if (Mode == Mode.None || Mode == Mode.MarkerSelected) {
				RedrawMapData ();
				ZoomToMyLocationAndAlarms ();

                if (Mode == Mode.MarkerSelected)
                {
                    ManageMenuItemsVisibilityForMode();
                }
			}
		}

		void ClearMap ()
		{
			foreach (var marker in _currentMarkers) {
				marker.Remove ();
			}

			_currentMarkers.Clear ();

			foreach (var circle in _currentCircles) {
				circle.Remove ();
			}

			_currentCircles.Clear ();

			if (AlarmToAddMarker != null) {
				AlarmToAddMarker.Remove ();
			}

			if (_selectedMarker != null) {
				_selectedMarker.Remove ();
			}
		}

		void RedrawMapData ()
		{
			if (_map == null) {
				return;
			}

			ClearMap ();

			foreach (var alarm in _mapData) {
				var position = new LatLng (alarm.Latitude, alarm.Longitude);
				var selected = _selectedMarker != null && _selectedMarker.Position.Latitude == position.Latitude && _selectedMarker.Position.Longitude == position.Longitude;

				var circle = _map.AddCircle (new CircleOptions ()
                    .InvokeCenter (position)
                    .InvokeRadius (alarm.Radius));

				circle.FillColor = Resources.GetColor (alarm.Enabled ? Resource.Color.light : Resource.Color.light_grey);
				circle.StrokeColor = Resources.GetColor (alarm.Enabled ? Resource.Color.dark : Resource.Color.dark_grey);
				circle.StrokeWidth = 1.0f;

				_currentCircles.Add (circle);
                                
				_currentMarkers.Add (_map.AddMarker (new MarkerOptions ()
                    .SetPosition (position)
                    .SetTitle (alarm.Name)
                    .InvokeIcon (selected ? _alarm_marker_selected : (alarm.Enabled ? _alarm_marker_normal : _alarm_marker_disabled))));

				if (selected) {
					_selectedMarker = _currentMarkers [_currentMarkers.Count - 1];
					_selectedMarker.ShowInfoWindow ();
					_selectedAlarm = alarm;
				}
			}

			Log.Debug (TAG, "data redrawn");
		}

		void ZoomToMyLocationAndAlarms ()
		{
			var location = MyCurrentLocation;

			if (_mapData.Count > 0) {
				var boundsBuilder = new LatLngBounds.Builder ();

				foreach (var alarm in _mapData) {
					boundsBuilder.Include (new LatLng (alarm.Latitude, alarm.Longitude));
				}

				if (location != null) {
					boundsBuilder.Include (new LatLng (location.Latitude, location.Longitude));
				}

				try {
					_map.AnimateCamera (CameraUpdateFactory.NewLatLngBounds (boundsBuilder.Build (), 200));
					Log.Debug (TAG, "map zoomed with NewLatLngBounds");
				} catch {
					Log.Debug (TAG, "exception while zooming with NewLatLngBounds");
				}
			} else {
				AnimateTo (location);
			}
		}

		void OnMapClick (object sender, GoogleMap.MapClickEventArgs e)
		{
			switch (Mode) {
			case Mode.Add:
				if (AlarmToAddMarker == null) {
					AlarmToAddMarker = _map.AddMarker (new MarkerOptions ().SetPosition (e.Point));
					AlarmToAddMarker.SetIcon (_alarm_marker_normal);
					AlarmToAddMarker.Draggable = true;
				} else {
					AlarmToAddMarker.Position = e.Point;
                    RedrawAddCircle();
				}
                    
				break;

			case Mode.MarkerSelected:
				if (_selectedMarker != null) {
					_selectedMarker.SetIcon (_selectedAlarm.Enabled ? _alarm_marker_normal : _alarm_marker_disabled);
					_selectedMarker = null;
				}

				Mode = Mode.None;

				break;
			}
		}		

		void OnMarkerClick (object sender, GoogleMap.MarkerClickEventArgs e)
		{
			switch (Mode) {
			case Mode.None:
			case Mode.MarkerSelected:
				if (_selectedMarker != null) {
					_selectedMarker.SetIcon (_selectedAlarm.Enabled ? _alarm_marker_normal : _alarm_marker_disabled);
				}

				_selectedMarker = e.Marker;
				_selectedMarker.SetIcon (_alarm_marker_selected);
				_selectedAlarm = _mapData.FirstOrDefault (a => a.Latitude == _selectedMarker.Position.Latitude && a.Longitude == _selectedMarker.Position.Longitude);
                    
				Mode = Mode.MarkerSelected;
				break;
			}

			e.Handled = false;
		}

		public void OnMapLoaded ()
		{
			_map.SetOnMapLoadedCallback (null);

			ZoomToMyLocationAndAlarms ();
		}

        void RedrawAddCircle()
        {
            if (_alarmToAdd != null)
            {
                if (_circleToAdd == null) {
                    _circleToAdd = _map.AddCircle(new CircleOptions().InvokeCenter(_alarmToAdd.Position));

                    _circleToAdd.FillColor = Resources.GetColor(Resource.Color.light);
                    _circleToAdd.StrokeColor = Resources.GetColor(Resource.Color.dark);
                    _circleToAdd.StrokeWidth = 1.0f;
                } else {
                    _circleToAdd.Center = _alarmToAdd.Position;
                }
                    
                _circleToAdd.Radius = (float)Constants.AlarmRadiusValues[_alarmRadiusSpinner.SelectedItemPosition];
            }
            else
            {
                if (_circleToAdd != null)
                {
                    _circleToAdd.Remove();
                    _circleToAdd = null;
                }
            }
        }
	}
}