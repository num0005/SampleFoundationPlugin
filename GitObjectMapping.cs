using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenBlamPlugin
{
    
    /// <summary>
    /// A bi-directional mapping from objects to integer IDs 
    /// </summary>
    internal class GitObjectMapping
    {

        public GitObjectMapping()
        {
            // map zero ID to itself
            numericalToObject[0] = ObjectId.Zero;
            objectToNumerical[ObjectId.Zero] = 0;
        }
        public int AddObject(GitObject gitObject)
        {
            // check if the object is already mapped
            if (objectToNumerical.ContainsKey(gitObject.Id))
                return objectToNumerical[gitObject.Id];

            int? numerical = null;
            byte[] ID = gitObject.Id.RawId;
            for (int i = 0; i < ID.Length - 4; i++)
            {
                int numericalTest = GenerateNumericalID(ID, i);
                // check if there is already an object with that ID
                // if not then we can use this mapping;
                if (IsNumercialIDInUse(numericalTest))
                {
                    numerical = numericalTest;
                    break;
                }
            }

            // fallback if the above fails to find a valid mapping
            if (numerical is null)
            {
                int seed = 0;
                for (int i = 0; i < ID.Length - 4; i += 4)
                {
                    seed ^= BitConverter.ToInt32(ID, i);
                }
                Random generator = new Random(seed);
                // Note(num0005): This could be an infinite loop but that's very unlikely
                while (numerical is null) {
                    int numericalID = generator.Next();
                    if (IsNumercialIDInUse(numericalID))
                    {
                        numerical = numericalID;
                        break;
                    }
                }
            }

            int mappingID = (int)numerical;
            numericalToObject[mappingID] = gitObject.Id;
            objectToNumerical[gitObject.Id] = mappingID;
            return mappingID;
        }

        private bool IsNumercialIDInUse(int ID)
        {
            ObjectId otherObject = GetObjectID(ID);
            return !(otherObject is null);
        }

        public ObjectId GetObjectID(int numericalID)
        {
            ObjectId objectId = null;
            numericalToObject.TryGetValue(numericalID, out objectId);
            return objectId;
        }

        private int GenerateNumericalID(byte[] ID, int startIndex = 0)
        {
            return BitConverter.ToInt32(ID, startIndex);
        }

        private Dictionary<int, ObjectId> numericalToObject = new Dictionary<int, ObjectId>();
        private Dictionary<ObjectId, int> objectToNumerical = new Dictionary<ObjectId, int>();
    }
}
