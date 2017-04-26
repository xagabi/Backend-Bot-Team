﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LuisBot.Repositories
{
    public interface IRepository<T>
    {
        PagedResult<T> RetrievePage(int pageNumber, int pageSize, Func<T, bool> predicate = default(Func<T, bool>));

        T GetByName(string name);
    }
}