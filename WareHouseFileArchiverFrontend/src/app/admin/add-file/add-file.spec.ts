import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AddFileComponent as AddFile } from './add-file';

describe('AddFile', () => {
  let component: AddFile;
  let fixture: ComponentFixture<AddFile>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AddFile]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AddFile);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
