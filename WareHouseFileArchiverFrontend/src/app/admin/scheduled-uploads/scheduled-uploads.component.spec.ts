import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CommonModule } from '@angular/common';
import { of, throwError } from 'rxjs';
import { ScheduledUploadsComponent } from './scheduled-uploads';
import { AdminService } from '../../services/admin.service';

describe('ScheduledUploadsComponent', () => {
  let component: ScheduledUploadsComponent;
  let fixture: ComponentFixture<ScheduledUploadsComponent>;
  let mockAdminService: jasmine.SpyObj<AdminService>;

  const mockScheduledUploads = [
    {
      id: '1',
      fileName: 'scheduled-file1.pdf',
      fileSizeInBytes: 1024,
      createdAt: '2024-01-15T10:00:00Z',
      isProcessed: false
    },
    {
      id: '2',
      fileName: 'processed-file.xlsx',
      fileSizeInBytes: 4096,
      createdAt: '2024-01-14T09:00:00Z',
      isProcessed: true
    }
  ];

  beforeEach(async () => {
    const adminServiceSpy = jasmine.createSpyObj('AdminService', [
      'getScheduledFiles',
      'cancelScheduledUpload'
    ]);

    await TestBed.configureTestingModule({
      imports: [CommonModule, ScheduledUploadsComponent],
      providers: [
        { provide: AdminService, useValue: adminServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ScheduledUploadsComponent);
    component = fixture.componentInstance;
    mockAdminService = TestBed.inject(AdminService) as jasmine.SpyObj<AdminService>;

    // Default spy returns
    mockAdminService.getScheduledFiles.and.returnValue(of(mockScheduledUploads));
    mockAdminService.cancelScheduledUpload.and.returnValue(of({ success: true }));
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load scheduled uploads on init', () => {
    spyOn(console, 'log');

    component.ngOnInit();
    fixture.detectChanges();

    expect(mockAdminService.getScheduledFiles).toHaveBeenCalled();
    expect(component.scheduledUploads).toEqual(mockScheduledUploads);
    expect(component.loading).toBeFalse();
  });

  it('should handle loading error', () => {
    spyOn(console, 'error');
    spyOn(window, 'alert');
    mockAdminService.getScheduledFiles.and.returnValue(throwError(() => new Error('Server error')));

    component.loadScheduledUploads();
    fixture.detectChanges();

    expect(component.loading).toBeFalse();
    expect(window.alert).toHaveBeenCalledWith('Failed to load scheduled uploads');
  });

  it('should cancel upload when user confirms', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    spyOn(window, 'alert');
    spyOn(component, 'loadScheduledUploads');

    component.cancelScheduledUpload('1', 'test-file.pdf');

    expect(mockAdminService.cancelScheduledUpload).toHaveBeenCalledWith('1');
    expect(window.alert).toHaveBeenCalledWith('Scheduled upload cancelled successfully');
    expect(component.loadScheduledUploads).toHaveBeenCalled();
  });

  it('should not cancel upload when user declines', () => {
    spyOn(window, 'confirm').and.returnValue(false);

    component.cancelScheduledUpload('1', 'test-file.pdf');

    expect(mockAdminService.cancelScheduledUpload).not.toHaveBeenCalled();
  });

  it('should handle cancel error', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    spyOn(console, 'error');
    spyOn(window, 'alert');
    mockAdminService.cancelScheduledUpload.and.returnValue(throwError(() => new Error('Cancel failed')));

    component.cancelScheduledUpload('1', 'test.pdf');

    expect(window.alert).toHaveBeenCalledWith('Failed to cancel scheduled upload');
  });

  it('should format file size correctly', () => {
    expect(component.formatFileSize(0)).toBe('0 Bytes');
    expect(component.formatFileSize(1024)).toBe('1 KB');
    expect(component.formatFileSize(1048576)).toBe('1 MB');
  });
});
