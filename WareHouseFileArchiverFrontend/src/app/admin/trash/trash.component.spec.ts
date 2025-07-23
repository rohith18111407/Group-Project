import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { TrashComponent } from './trash';
import { AdminService } from '../../services/admin.service';
import { TrashFile, TrashStats } from '../../models/trash.model';

describe('TrashComponent', () => {
  let component: TrashComponent;
  let fixture: ComponentFixture<TrashComponent>;
  let mockAdminService: jasmine.SpyObj<AdminService>;

  const mockTrashFiles: TrashFile[] = [
    {
      id: '1',
      fileName: 'test-file1.pdf',
      fileExtension: '.pdf',
      fileSizeInBytes: 1024,
      versionNumber: 1,
      description: 'Test description 1',
      category: 'Documents',
      itemId: 'item1',
      itemName: 'Test Item 1',
      createdAt: '2024-01-15T10:00:00Z',
      createdBy: 'admin',
      deletedAt: '2024-01-15T12:00:00Z',
      deletedBy: 'admin',
      daysInTrash: 2,
      daysRemaining: 5,
      canRestore: true
    },
    {
      id: '2',
      fileName: 'expired-file.docx',
      fileExtension: '.docx',
      fileSizeInBytes: 2048,
      versionNumber: 2,
      description: 'Test description 2',
      category: 'Reports',
      itemId: 'item2',
      itemName: 'Test Item 2',
      createdAt: '2024-01-10T10:00:00Z',
      createdBy: 'user1',
      deletedAt: '2024-01-10T12:00:00Z',
      deletedBy: 'user1',
      daysInTrash: 7,
      daysRemaining: 0,
      canRestore: false
    }
  ];

  const mockTrashStats: TrashStats = {
    totalTrashedFiles: 2,
    filesExpiringSoon: 1,
    totalSizeInBytes: 3072,
    oldestFileDate: '2024-01-10T12:00:00Z'
  };

  beforeEach(async () => {
    const adminServiceSpy = jasmine.createSpyObj('AdminService', [
      'getTrashedFiles',
      'getTrashStats',
      'restoreFromTrash',
      'permanentlyDeleteFromTrash',
      'forceCleanupTrash'
    ]);

    await TestBed.configureTestingModule({
      imports: [CommonModule, FormsModule, TrashComponent],
      providers: [
        { provide: AdminService, useValue: adminServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TrashComponent);
    component = fixture.componentInstance;
    mockAdminService = TestBed.inject(AdminService) as jasmine.SpyObj<AdminService>;

    // Default spy returns
    mockAdminService.getTrashedFiles.and.returnValue(of(mockTrashFiles));
    mockAdminService.getTrashStats.and.returnValue(of(mockTrashStats));
    mockAdminService.restoreFromTrash.and.returnValue(of({}));
    mockAdminService.permanentlyDeleteFromTrash.and.returnValue(of({}));
    mockAdminService.forceCleanupTrash.and.returnValue(of({}));
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load trashed files and stats on init', () => {
    component.ngOnInit();
    fixture.detectChanges();

    expect(mockAdminService.getTrashedFiles).toHaveBeenCalled();
    expect(mockAdminService.getTrashStats).toHaveBeenCalled();
    expect(component.trashedFiles).toEqual(mockTrashFiles);
    expect(component.trashStats).toEqual(mockTrashStats);
  });

  it('should handle loading error', () => {
    mockAdminService.getTrashedFiles.and.returnValue(throwError(() => new Error('Server error')));

    component.loadTrashedFiles();
    fixture.detectChanges();

    expect(component.loading).toBeFalse();
    expect(component.error).toBe('Failed to load trashed files. Please try again.');
  });

  it('should filter files by search term', () => {
    component.trashedFiles = mockTrashFiles;
    const event = { target: { value: 'test-file1' } } as any;

    component.onSearchChange(event);

    expect(component.searchTerm).toBe('test-file1');
    expect(component.filteredTrashedFiles.length).toBe(1);
    expect(component.filteredTrashedFiles[0].fileName).toBe('test-file1.pdf');
  });

  it('should restore file successfully', () => {
    spyOn(component, 'loadTrashedFiles');
    spyOn(component, 'loadTrashStats');
    
    component.selectedFile = mockTrashFiles[0];
    component.confirmAction = 'restore';

    component.executeAction();

    expect(mockAdminService.restoreFromTrash).toHaveBeenCalledWith(mockTrashFiles[0].id);
    expect(component.loadTrashedFiles).toHaveBeenCalled();
    expect(component.loadTrashStats).toHaveBeenCalled();
  });

  it('should permanently delete file successfully', () => {
    spyOn(component, 'loadTrashedFiles');
    spyOn(component, 'loadTrashStats');
    
    component.selectedFile = mockTrashFiles[0];
    component.confirmAction = 'permanentDelete';

    component.executeAction();

    expect(mockAdminService.permanentlyDeleteFromTrash).toHaveBeenCalledWith(mockTrashFiles[0].id);
    expect(component.loadTrashedFiles).toHaveBeenCalled();
    expect(component.loadTrashStats).toHaveBeenCalled();
  });

  it('should handle action errors', () => {
    component.selectedFile = mockTrashFiles[0];
    component.confirmAction = 'restore';
    mockAdminService.restoreFromTrash.and.returnValue(throwError(() => new Error('Restore failed')));

    component.executeAction();

    expect(component.error).toBe('Failed to restore file. Please try again.');
    expect(component.showConfirmDialog).toBeFalse();
  });
});
