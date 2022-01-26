using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static BBDown.BBDownEntity;
using static BBDown.BBDownUtil;

namespace BBDown
{
    class BBDownNormalInfoFetcher : IFetcher
    {
        public async Task<BBDownVInfo> FetchAsync(string id)
        {
            string api = $"https://api.bilibili.com/x/web-interface/view?aid={id}";
            string json = await GetWebSourceAsync(api);
            using var infoJson = JsonDocument.Parse(json);
            var data = infoJson.RootElement.GetProperty("data");
            string aid = data.GetProperty("aid").ToString();
            string bvid = data.GetProperty("bvid").ToString();
            string title = data.GetProperty("title").ToString();
            string desc = data.GetProperty("desc").ToString();
            string pic = data.GetProperty("pic").ToString();
            string pubTime = data.GetProperty("pubdate").ToString();
            pubTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Convert.ToDouble(pubTime)).AddHours(8.0).ToString();  //始终使用UTC+8
            var owner = data.GetProperty("owner");
            string ownerMid = owner.GetProperty("mid").ToString();
            string ownerName = owner.GetProperty("name").ToString();
            bool bangumi = false;

            var staffElem = new JsonElement();
            var staff = new List<StaffMember>();
            if (data.TryGetProperty("staff", out staffElem))
            {
                foreach (var memberElem in staffElem.EnumerateArray())
                {
                    var member = new StaffMember();
                    member.Name = memberElem.GetProperty("name").ToString();
                    member.Mid = memberElem.GetProperty("mid").ToString();
                    member.Title = memberElem.GetProperty("title").ToString();
                    staff.Add(member);
                }
            }
            else
            {
                var member = new StaffMember();
                member.Name = ownerName;
                member.Mid = ownerMid;
                member.Title = "UP主";
                staff.Append(member);
            }

            var pages = data.GetProperty("pages").EnumerateArray().ToList();
            List<Page> pagesInfo = new List<Page>();
            string rootSourceUrl = $"https://www.bilibili.com/video/{bvid}";
            foreach (var page in pages)
            {
                int pageNum = page.GetProperty("page").GetInt32();
                string sourceUrl = (pages.Count() <= 1) ? rootSourceUrl : rootSourceUrl + $"?p={pageNum}";
                Page p = new Page(pageNum,
                    id,
                    page.GetProperty("cid").ToString(),
                    "", //epid
                    page.GetProperty("part").ToString().Trim(),
                    page.GetProperty("duration").GetInt32(),
                    page.GetProperty("dimension").GetProperty("width").ToString() + "x" + page.GetProperty("dimension").GetProperty("height").ToString(),
                    sourceUrl
                    );
                pagesInfo.Add(p);
            }

            try
            {
                if (data.GetProperty("redirect_url").ToString().Contains("bangumi"))
                {
                    bangumi = true;
                    string epId = Regex.Match(data.GetProperty("redirect_url").ToString(), "ep(\\d+)").Groups[1].Value;
                    //番剧内容通常不会有分P，如果有分P则不需要epId参数
                    if (pages.Count == 1)
                    {
                        pagesInfo.ForEach(p => p.epid = epId);
                    }
                }
            }
            catch { }

            var info = new BBDownVInfo();
            info.Aid = aid;
            info.Bvid = bvid;
            info.Title = title.Trim();
            info.Desc = desc.Trim();
            info.Pic = pic;
            info.PubTime = pubTime;
            info.PagesInfo = pagesInfo;
            info.OwnerMid = ownerMid;
            info.OwnerName = ownerName;
            info.Staff = staff;
            info.IsBangumi = bangumi;

            return info;
        }
    }
}
