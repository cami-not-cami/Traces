using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Traces.Models;

namespace Traces.Controllers
{
    public class ChecklistsController : Controller
    {
        private readonly TracesDbContext _context;

        public ChecklistsController(TracesDbContext context)
        {
            _context = context;
        }

        // GET: Checklists
        public async Task<IActionResult> Index()
        {
            var tracesDbContext = _context.Checklists.Include(c => c.TripFkNavigation);
            return View(await tracesDbContext.ToListAsync());
        }

        // GET: Checklists/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var checklist = await _context.Checklists
                .Include(c => c.TripFkNavigation)
                .FirstOrDefaultAsync(m => m.ChIdPk == id);
            if (checklist == null)
            {
                return NotFound();
            }

            return View(checklist);
        }

        // GET: Checklists/Create
        public IActionResult Create()
        {
            ViewData["TripFk"] = new SelectList(_context.Trips, "TrIdPk", "Title");
            return View();
        }

        // POST: Checklists/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ChIdPk,TripFk,Content,IsCompleted,OrderIndex")] Checklist checklist)
        {
            if (ModelState.IsValid)
            {
                _context.Add(checklist);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["TripFk"] = new SelectList(_context.Trips, "TrIdPk", "Title", checklist.TripFk);
            return View(checklist);
        }

        // GET: Checklists/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var checklist = await _context.Checklists.FindAsync(id);
            if (checklist == null)
            {
                return NotFound();
            }
            ViewData["TripFk"] = new SelectList(_context.Trips, "TrIdPk", "Title", checklist.TripFk);
            return View(checklist);
        }

        // POST: Checklists/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ChIdPk,TripFk,Content,IsCompleted,OrderIndex")] Checklist checklist)
        {
            if (id != checklist.ChIdPk)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(checklist);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ChecklistExists(checklist.ChIdPk))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["TripFk"] = new SelectList(_context.Trips, "TrIdPk", "Title", checklist.TripFk);
            return View(checklist);
        }

        // GET: Checklists/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var checklist = await _context.Checklists
                .Include(c => c.TripFkNavigation)
                .FirstOrDefaultAsync(m => m.ChIdPk == id);
            if (checklist == null)
            {
                return NotFound();
            }

            return View(checklist);
        }

        // POST: Checklists/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var checklist = await _context.Checklists.FindAsync(id);
            if (checklist != null)
            {
                _context.Checklists.Remove(checklist);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ChecklistExists(int id)
        {
            return _context.Checklists.Any(e => e.ChIdPk == id);
        }
    }
}
