﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DroneTransferSimulator
{
    public class Simulator
    {
        /* singleton instance for Simulator */
        private static Simulator instance;

        private List<Event> events = new List<Event>();
        private SortedList<int, Event> eventsQueue = new SortedList<int, Event>();
        private Dictionary<string, DroneStation> stationDict = new Dictionary<string, DroneStation>();
        private List<DroneStation> stations = new List<DroneStation>();
        
        public void getEventList(ref List<Event> eventList)
        {
            eventList = events;
        }

        public void getStationList(ref List<DroneStation> stationList)
        {
            stationList = stations;
        }

        public Dictionary<string, DroneStation> getStationDict()
        {
            return stationDict;
        }

        public string getEventsFromCSV(string fpath)
        {
            try
            {
                if(events.Count != 0) events.Clear();

                System.IO.StreamReader readFile = new System.IO.StreamReader(fpath);
                while(!readFile.EndOfStream)
                {
                    var line = readFile.ReadLine();
                    var record = line.Split(',');
                    if(record.Length != 6) throw new Exception("Inappropriate CSV format\nCannot be read");

                    Time occuredDate = new Time();
                    Time ambulDate = new Time();
                    double longitude = System.Convert.ToDouble(record[0]);
                    double latitude = System.Convert.ToDouble(record[1]);
                    occuredDate.year = System.Convert.ToInt32(record[2]) / 10000;
                    occuredDate.month = (System.Convert.ToInt32(record[2]) % 10000) / 100;
                    occuredDate.date = System.Convert.ToInt32(record[2]) % 100;
                    occuredDate.hour = System.Convert.ToInt32(record[3]) / 100;
                    occuredDate.min = System.Convert.ToInt32(record[3]) % 100;

                    ambulDate.year = System.Convert.ToInt32(record[4]) / 10000;
                    ambulDate.month = (System.Convert.ToInt32(record[4]) % 10000) / 100;
                    ambulDate.date = System.Convert.ToInt32(record[4]) % 100;
                    ambulDate.hour = System.Convert.ToInt32(record[5]) / 100;
                    ambulDate.min = System.Convert.ToInt32(record[5]) % 100;

                    Event.eventType e = new Event.eventType();
                    e = Event.eventType.E_EVENT_OCCURED;
                    events.Add(new Event(latitude, longitude, occuredDate, ambulDate, e));
                }
                readFile.Close();
            }
            catch(Exception e)
            {
                return e.Message;
            }
            return null;
        }


        public string getStationsFromCSV(string fpath)
        {
            try
            {
                if(stations.Count != 0) stations.Clear();
                if(stationDict.Count != 0) stationDict.Clear();

                System.IO.StreamReader readFile = new System.IO.StreamReader(fpath);
                while(!readFile.EndOfStream)
                {
                    var line = readFile.ReadLine();
                    var record = line.Split(',');
                    if(record.Length != 4) throw new Exception("Inappropriate CSV format\nCannot be read");

                    string name = record[0];
                    double latitude = System.Convert.ToDouble(record[1]);
                    double longitude = System.Convert.ToDouble(record[2]);
                    double coverRange = System.Convert.ToDouble(record[3]);

                    stations.Add(new DroneStation(name, latitude, longitude, coverRange));
                    stationDict.Add(name, new DroneStation(name, latitude, longitude, coverRange));
                }
                readFile.Close();
            }
            catch(Exception e)
            {
                return e.Message;
            }
            return null;
        }

        public void updateEventsBtwRange(Time start, Time end)
        {
            if (Time.timeComparator(end, start)) return;
            events.Sort();

            int startIndex = 0, endIndex = 0;
            foreach (Event eventElement in events)
            {
                if (Time.timeComparator(eventElement.getOccuredDate(), start))
                {
                    startIndex++;
                    endIndex++;
                }
                else if (Time.timeComparator(eventElement.getOccuredDate(), end)) endIndex++;
                else break;
            }

            if (endIndex == startIndex + 1) return; // no events
            //events.RemoveRange(endIndex, events.Count-1);
            //events.RemoveRange(0, startIndex);

        }
        public void start()
        {
            foreach (Event eventElement in events)
            {
                int date = eventElement.getOccuredDate().min + 100 * (eventElement.getOccuredDate().hour + 100 * (eventElement.getOccuredDate().date + 100 * (eventElement.getOccuredDate().month + 100 * eventElement.getOccuredDate().year)));
                eventsQueue.Add(date, eventElement);
            }

            StationManager.getStations(ref stations);

            while (eventsQueue.Count != 0)
            {
                Event e = eventsQueue.Values[0];
                eventsQueue.RemoveAt(0);

                switch (e.getEventType())
                {
                    case Event.eventType.E_EVENT_OCCURED:
                        eventOccured(e.getCoordinates(), e.getOccuredDate());
                        break;

                    case Event.eventType.E_EVENT_ARRIVAL:
                        eventArrived(e.getCoordinates(), e.getOccuredDate(), e.getStationDroneIdx());
                        break;
                    case Event.eventType.E_STATION_ARRIVAL:
                        stationArrival(e.getOccuredDate(), e.getStationDroneIdx());
                        break;
                }
            }
        }
        public void eventOccured(Tuple<double, double> coordinates, Time occuredTime)
        {
            //find stations and drone
            
            DroneStationFinder finder = new DroneStationFinder(coordinates);
            finder.findAvailableStations();
            Tuple<int, int> stationDroneIdx = finder.findAvailableDrone(occuredTime);
            if (stationDroneIdx.Item1 == -1) return;

            DroneStation s = stations[stationDroneIdx.Item1];
            Drone d = s.drones[stationDroneIdx.Item2];

            double distance = finder.getDistanceFromRecentEvent(s.stationLng, s.stationLat);

            //calculate time
            PathPlanner pathPlanner = PathPlanner.getInstance();
            double calculatedTime;
            calculatedTime = pathPlanner.calcTravelTime(s.stationLat, s.stationLng, coordinates.Item2, coordinates.Item1);

            Time droneArrivalTime = Time.timeAdding(occuredTime, calculatedTime);

            //battery consumption
            d.fly(distance);
            d.setStatus(1);

            //declare coming event
            Event.eventType type = Event.eventType.E_EVENT_ARRIVAL;
            Event e = new Event(coordinates.Item1, coordinates.Item2, droneArrivalTime, droneArrivalTime, type);
            e.setStationDroneIdx(stationDroneIdx.Item1, stationDroneIdx.Item2);
            
            int date = e.getOccuredDate().min + 100 * (e.getOccuredDate().hour + 100 * (e.getOccuredDate().date + 100 * (e.getOccuredDate().month + 100 * e.getOccuredDate().year)));
            eventsQueue.Add(date, e);
        }
        public void eventArrived(Tuple<double, double> occuredCoord, Time occuredTime, Tuple<int, int> stationDroneIdx)
        {
            //time to return to the Drone Station
            PathPlanner pathPlanner = PathPlanner.getInstance();
            double calculatedTime;
            calculatedTime = pathPlanner.calcTravelTime(occuredCoord.Item2, occuredCoord.Item1, stations[stationDroneIdx.Item1].stationLat, stations[stationDroneIdx.Item1].stationLng);

            //time when drone reach the station
            Time droneArrivalTime = Time.timeAdding(occuredTime, calculatedTime);


            //battery consumption
            DroneStationFinder f = new DroneStationFinder(new Tuple<double, double>(occuredCoord.Item1, occuredCoord.Item2));
            double distance = f.getDistanceFromRecentEvent(stations[stationDroneIdx.Item1].stationLng, stations[stationDroneIdx.Item1].stationLat);
            stations[stationDroneIdx.Item1].drones[stationDroneIdx.Item2].fly(distance);

            //event of arriving
            Event.eventType type = Event.eventType.E_STATION_ARRIVAL;
            Event e = new Event(occuredCoord.Item1, occuredCoord.Item2, droneArrivalTime, droneArrivalTime, type);
            e.setStationDroneIdx(stationDroneIdx.Item1, stationDroneIdx.Item2);

            int date = e.getOccuredDate().min + 100 * (e.getOccuredDate().hour + 100 * (e.getOccuredDate().date + 100 * (e.getOccuredDate().month + 100 * e.getOccuredDate().year)));
            eventsQueue.Add(date, e);
        }
        public void stationArrival(Time arrivalTime, Tuple<int, int> stationDroneIdx)
        {
            Drone d = stations[stationDroneIdx.Item1].drones[stationDroneIdx.Item2];
            d.setStatus(2);
            d.setChargeStartTime(arrivalTime);
        }
        
        public static Simulator getInstance()
        {
            if(instance == null) instance = new Simulator();
            return instance;
        }
    }
}
