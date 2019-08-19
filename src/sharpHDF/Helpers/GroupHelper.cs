﻿/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * Copyright by Brian Nelson 2016.                                           *
 * See license in repo for more information                                  *
 * https://github.com/sharpHDF/sharpHDF                                      *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HDF.PInvoke;
using sharpHDF.Library.Interfaces;
using sharpHDF.Library.Objects;
using sharpHDF.Library.Structs;

namespace sharpHDF.Library.Helpers
{
    internal static class GroupHelper
    {
        public static void PopulateChildrenObjects<T>(Hdf5Identifier _fileId, T _parentObject)  where T : AbstractHdf5Object
        {
            ulong pos = 0;

            List<string> groupNames = new List<string>();

            var id = H5G.open(_fileId.Value, _parentObject.Path.FullPath).ToId();

            if (id.Value > 0)
            {
                ArrayList al = new ArrayList();
                GCHandle hnd = GCHandle.Alloc(al);
                IntPtr op_data = (IntPtr) hnd;

                H5L.iterate(_parentObject.Id.Value, H5.index_t.NAME, H5.iter_order_t.NATIVE, ref pos,
                    delegate(long _objectId, IntPtr _namePtr, ref H5L.info_t _info, IntPtr _data)
                    {
                        string objectName = Marshal.PtrToStringAnsi(_namePtr);

                        groupNames.Add(objectName);

                        return 0;
                    }, op_data);

                hnd.Free();

                H5G.close(id.Value);

                foreach (var groupName in groupNames)
                {
                    Object hdf5Obj = GetObject(_fileId, _parentObject, groupName);

                    if (hdf5Obj != null)
                    {
                        if (hdf5Obj is Hdf5Dataset)
                        {

                            if (_parentObject is IHasDatasets parent) {
                                parent.Datasets.Add(hdf5Obj as Hdf5Dataset);
                            }
                        }
                        else if (hdf5Obj is Hdf5Group)
                        {

                            if (_parentObject is IHasGroups parent) {
                                parent.Groups.Add(hdf5Obj as Hdf5Group);
                            }
                        }
                    }
                }
            }
        }

        public static object GetObject(Hdf5Identifier _fileId, AbstractHdf5Object _parent, string _objectName)
        {
            Hdf5Path combinedPath = _parent.Path.Append(_objectName);
            object output = null;

            if (combinedPath != null)
            {
                string fullPath = combinedPath.FullPath;

                H5O.info_t gInfo = new H5O.info_t();
                H5O.get_info_by_name(_fileId.Value, fullPath, ref gInfo);

                var id = H5O.open(_fileId.Value, fullPath).ToId();
                if (id.Value > 0)
                {
                    if (gInfo.type == H5O.type_t.DATASET)
                    {
                        output = DatasetHelper.LoadDataset(_fileId, id, fullPath);
                    }

                    if (gInfo.type == H5O.type_t.GROUP)
                    {
                        Hdf5Group group = new Hdf5Group(_fileId, id, fullPath) {
                            FileId = _fileId
                        };
                        group.LoadChildObjects();
                        output = group;
                    }

                    H5O.close(id.Value);
                }
            }

            return output;
        }

        public static Hdf5Group CreateGroupAddToList(
            ReadonlyNamedItemList<Hdf5Group> _groups, 
            Hdf5Identifier _fileId,
            Hdf5Path _parentPath, 
            string _name)
        {
            Hdf5Group group = CreateGroup(_fileId, _parentPath, _name);

            if (group != null)
            {
                _groups.Add(group);
            }
            return group;
        }

        public static Hdf5Group CreateGroup(Hdf5Identifier _fileId, Hdf5Path _parentPath, string _name)
        {
            Hdf5Path path = _parentPath.Append(_name);

            long id = H5G.create(_fileId.Value, path.FullPath);

            if (id > 0)
            {
                Hdf5Group group = new Hdf5Group(_fileId, id.ToId(), path.FullPath);
                H5G.close(id);

                FileHelper.FlushToFile(_fileId);

                return group;
            }

            return null;
        }

    }
}
